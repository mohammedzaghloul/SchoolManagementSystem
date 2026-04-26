using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using School.Application.Interfaces;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.Globalization;
using System.Text.Json;

namespace School.Infrastructure.Services;

public sealed class OnnxFaceRecognitionService : IFaceRecognitionService, IDisposable
{
    private const string DefaultModelFileName = "arcface02.onnx";
    private const string DefaultEmbeddingDbFileName = "face_data.db";
    private const float DefaultSimilarityThreshold = 0.35f;

    private readonly ILogger<OnnxFaceRecognitionService> _logger;
    private readonly string _embeddingDbPath;
    private readonly float _similarityThreshold;
    private readonly Lazy<ArcFaceModelContext> _modelContext;
    private bool _disposed;

    public OnnxFaceRecognitionService(
        IConfiguration configuration,
        IHostEnvironment environment,
        ILogger<OnnxFaceRecognitionService> logger)
    {
        _logger = logger;
        _embeddingDbPath = ResolveEmbeddingDbPath(configuration, environment);
        _similarityThreshold = configuration.GetValue<float?>("FaceRecognition:SimilarityThreshold") ?? DefaultSimilarityThreshold;
        _modelContext = new Lazy<ArcFaceModelContext>(
            () => LoadModelContext(configuration, environment),
            LazyThreadSafetyMode.ExecutionAndPublication);

        SQLitePCL.Batteries_V2.Init();
        EnsureEmbeddingStore();
    }

    public async Task<FaceTrainingResult> TrainFaceAsync(int studentId, byte[] imageBytes, string fileName)
    {
        try
        {
            var embedding = await Task.Run(() => GenerateEmbedding(imageBytes));
            if (embedding.Length == 0)
            {
                return new FaceTrainingResult
                {
                    Success = false,
                    Message = "تعذر استخراج بصمة واضحة من الصورة. قرّب الوجه من الكاميرا ثم حاول مرة أخرى."
                };
            }

            SaveEmbedding(studentId, embedding);

            return new FaceTrainingResult
            {
                Success = true,
                Message = "تم تسجيل بصمة الوجه بنجاح."
            };
        }
        catch (UnknownImageFormatException ex)
        {
            _logger.LogWarning(ex, "Invalid image format while training face for student {StudentId}.", studentId);
            return new FaceTrainingResult
            {
                Success = false,
                Message = "ملف الصورة غير صالح. التقط صورة جديدة ثم حاول مرة أخرى."
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ONNX face training failed for student {StudentId}.", studentId);
            return new FaceTrainingResult
            {
                Success = false,
                Message = "تعذر تسجيل الوجه حاليًا. تأكد من وجود الموديل وصلاحية الصورة ثم حاول مرة أخرى."
            };
        }
    }

    public async Task<FaceRecognitionResult> RecognizeFaceAsync(byte[] imageBytes, string fileName)
    {
        try
        {
            var probeEmbedding = await Task.Run(() => GenerateEmbedding(imageBytes));
            if (probeEmbedding.Length == 0)
            {
                return new FaceRecognitionResult
                {
                    Success = false,
                    Message = "لم نتمكن من استخراج بصمة واضحة من الصورة الحالية. قرّب الوجه من الكاميرا ثم حاول مرة أخرى."
                };
            }

            var storedEmbeddings = LoadEmbeddings(probeEmbedding.Length);
            if (storedEmbeddings.Count == 0)
            {
                return new FaceRecognitionResult
                {
                    Success = false,
                    Message = "لا توجد بصمات وجوه متوافقة مع الموديل الحالي. أعد تدريب وجوه الطلاب المطلوبين أولًا."
                };
            }

            var bestMatch = storedEmbeddings
                .Select(stored => new
                {
                    stored.StudentId,
                    Similarity = CosineSimilarity(probeEmbedding, stored.Embedding)
                })
                .OrderByDescending(match => match.Similarity)
                .First();

            if (bestMatch.Similarity < _similarityThreshold)
            {
                return new FaceRecognitionResult
                {
                    Success = false,
                    Message = "لم يتم التعرف على الوجه بثقة كافية. أعد تدريب الوجه بصورة أوضح أو جرّب إضاءة أفضل."
                };
            }

            return new FaceRecognitionResult
            {
                Success = true,
                StudentId = bestMatch.StudentId,
                Confidence = Math.Round(bestMatch.Similarity, 4),
                Message = "تم التعرف على الوجه بنجاح."
            };
        }
        catch (UnknownImageFormatException ex)
        {
            _logger.LogWarning(ex, "Invalid image format while recognizing face.");
            return new FaceRecognitionResult
            {
                Success = false,
                Message = "ملف الصورة غير صالح. أعد التقاط الصورة ثم حاول مرة أخرى."
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ONNX face recognition failed.");
            return new FaceRecognitionResult
            {
                Success = false,
                Message = "تعذر التعرف على الوجه حاليًا. تأكد من وجود الموديل ثم حاول مرة أخرى."
            };
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (_modelContext.IsValueCreated)
        {
            _modelContext.Value.Session.Dispose();
        }

        _disposed = true;
    }

    private ArcFaceModelContext LoadModelContext(IConfiguration configuration, IHostEnvironment environment)
    {
        var modelPath = ResolveModelPath(configuration, environment);
        EnsureModelAvailable(configuration, modelPath);
        if (string.IsNullOrWhiteSpace(modelPath) || !File.Exists(modelPath))
        {
            throw new FileNotFoundException($"ArcFace ONNX model was not found. Resolved path: {modelPath}", modelPath);
        }

        var sessionOptions = new SessionOptions
        {
            GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL
        };

        var session = new InferenceSession(modelPath, sessionOptions);
        var input = session.InputMetadata.First();
        var dimensions = input.Value.Dimensions.ToArray();
        var layout = dimensions.Length == 4 && dimensions[1] == 3
            ? TensorLayout.Nchw
            : TensorLayout.Nhwc;

        var height = layout == TensorLayout.Nchw
            ? NormalizeDimension(dimensions.ElementAtOrDefault(2), 112)
            : NormalizeDimension(dimensions.ElementAtOrDefault(1), 112);
        var width = layout == TensorLayout.Nchw
            ? NormalizeDimension(dimensions.ElementAtOrDefault(3), 112)
            : NormalizeDimension(dimensions.ElementAtOrDefault(2), 112);

        _logger.LogInformation(
            "Loaded ArcFace ONNX model from {ModelPath}. Input: {InputName}, layout: {Layout}, size: {Width}x{Height}.",
            modelPath,
            input.Key,
            layout,
            width,
            height);

        return new ArcFaceModelContext(session, input.Key, layout, width, height);
    }

    private void EnsureModelAvailable(IConfiguration configuration, string modelPath)
    {
        if (string.IsNullOrWhiteSpace(modelPath) || File.Exists(modelPath))
        {
            return;
        }

        var bucketName = configuration["FaceRecognition:S3BucketName"];
        var objectKey = configuration["FaceRecognition:S3ObjectKey"] ?? DefaultModelFileName;
        var endpoint = configuration["FaceRecognition:S3Endpoint"];
        var region = configuration["FaceRecognition:S3Region"] ?? "auto";
        var accessKeyId = configuration["FaceRecognition:S3AccessKeyId"];
        var secretAccessKey = configuration["FaceRecognition:S3SecretAccessKey"];

        if (string.IsNullOrWhiteSpace(bucketName) ||
            string.IsNullOrWhiteSpace(objectKey) ||
            string.IsNullOrWhiteSpace(endpoint) ||
            string.IsNullOrWhiteSpace(accessKeyId) ||
            string.IsNullOrWhiteSpace(secretAccessKey))
        {
            _logger.LogWarning("ArcFace ONNX model was not found locally and bucket download settings are incomplete.");
            return;
        }

        var directory = Path.GetDirectoryName(modelPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = $"{modelPath}.download";
        if (File.Exists(tempPath))
        {
            File.Delete(tempPath);
        }

        try
        {
            var config = new AmazonS3Config
            {
                ServiceURL = endpoint,
                AuthenticationRegion = region,
                ForcePathStyle = true
            };

            using var client = new AmazonS3Client(accessKeyId, secretAccessKey, config);
            using var response = client.GetObjectAsync(new GetObjectRequest
            {
                BucketName = bucketName,
                Key = objectKey
            }).GetAwaiter().GetResult();

            using (var output = File.Create(tempPath))
            {
                response.ResponseStream.CopyTo(output);
            }

            if (File.Exists(modelPath))
            {
                File.Delete(modelPath);
            }

            File.Move(tempPath, modelPath);

            _logger.LogInformation(
                "Downloaded ArcFace ONNX model from bucket {BucketName}/{ObjectKey} to {ModelPath}.",
                bucketName,
                objectKey,
                modelPath);
        }
        catch (Exception ex)
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }

            _logger.LogWarning(
                ex,
                "Failed to download ArcFace ONNX model from bucket {BucketName}/{ObjectKey}.",
                bucketName,
                objectKey);
        }
    }

    private float[] GenerateEmbedding(byte[] imageBytes)
    {
        var model = _modelContext.Value;

        using var image = Image.Load<Rgb24>(imageBytes);
        image.Mutate(context => context.AutoOrient());

        var cropSize = Math.Min(image.Width, image.Height);
        var cropX = Math.Max(0, (image.Width - cropSize) / 2);
        var cropY = Math.Max(0, (image.Height - cropSize) / 2);

        using var cropped = image.Clone(context => context
            .Crop(new Rectangle(cropX, cropY, cropSize, cropSize))
            .Resize(model.Width, model.Height));

        var inputTensor = CreateInputTensor(cropped, model);

        using var results = model.Session.Run(
            [
                NamedOnnxValue.CreateFromTensor(model.InputName, inputTensor)
            ]);

        var output = results.FirstOrDefault();
        if (output == null)
        {
            return [];
        }

        var embedding = output.AsEnumerable<float>().ToArray();
        return NormalizeEmbedding(embedding);
    }

    private static DenseTensor<float> CreateInputTensor(Image<Rgb24> image, ArcFaceModelContext model)
    {
        if (model.Layout == TensorLayout.Nchw)
        {
            var tensor = new DenseTensor<float>([1, 3, model.Height, model.Width]);
            for (var y = 0; y < model.Height; y++)
            {
                for (var x = 0; x < model.Width; x++)
                {
                    var pixel = image[x, y];
                    tensor[0, 0, y, x] = NormalizeChannel(pixel.R);
                    tensor[0, 1, y, x] = NormalizeChannel(pixel.G);
                    tensor[0, 2, y, x] = NormalizeChannel(pixel.B);
                }
            }

            return tensor;
        }

        var nhwcTensor = new DenseTensor<float>([1, model.Height, model.Width, 3]);
        for (var y = 0; y < model.Height; y++)
        {
            for (var x = 0; x < model.Width; x++)
            {
                var pixel = image[x, y];
                nhwcTensor[0, y, x, 0] = NormalizeChannel(pixel.R);
                nhwcTensor[0, y, x, 1] = NormalizeChannel(pixel.G);
                nhwcTensor[0, y, x, 2] = NormalizeChannel(pixel.B);
            }
        }

        return nhwcTensor;
    }

    private void EnsureEmbeddingStore()
    {
        var directory = Path.GetDirectoryName(_embeddingDbPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var connection = CreateConnection();
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS face_embeddings (
                student_id INTEGER PRIMARY KEY,
                embedding TEXT NOT NULL,
                updated_at TEXT NULL
            );
            """;
        command.ExecuteNonQuery();
    }

    private void SaveEmbedding(int studentId, float[] embedding)
    {
        using var connection = CreateConnection();
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO face_embeddings (student_id, embedding, updated_at)
            VALUES ($studentId, $embedding, $updatedAt)
            ON CONFLICT(student_id) DO UPDATE SET
                embedding = excluded.embedding,
                updated_at = excluded.updated_at;
            """;
        command.Parameters.AddWithValue("$studentId", studentId);
        command.Parameters.AddWithValue("$embedding", JsonSerializer.Serialize(embedding));
        command.Parameters.AddWithValue("$updatedAt", DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
        command.ExecuteNonQuery();
    }

    private List<StoredEmbedding> LoadEmbeddings(int expectedLength)
    {
        var embeddings = new List<StoredEmbedding>();

        using var connection = CreateConnection();
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT student_id, embedding FROM face_embeddings";

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var studentId = reader.GetInt32(0);
            var serialized = reader.IsDBNull(1) ? null : reader.GetString(1);
            if (string.IsNullOrWhiteSpace(serialized))
            {
                continue;
            }

            try
            {
                var vector = JsonSerializer.Deserialize<float[]>(serialized);
                if (vector is { Length: > 0 } && vector.Length == expectedLength)
                {
                    embeddings.Add(new StoredEmbedding(studentId, NormalizeEmbedding(vector)));
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Skipping invalid embedding for student {StudentId}.", studentId);
            }
        }

        return embeddings;
    }

    private SqliteConnection CreateConnection()
    {
        return new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = _embeddingDbPath,
            Mode = SqliteOpenMode.ReadWriteCreate
        }.ToString());
    }

    private static float[] NormalizeEmbedding(float[] embedding)
    {
        if (embedding.Length == 0)
        {
            return embedding;
        }

        var norm = MathF.Sqrt(embedding.Sum(value => value * value));
        if (norm <= 0f)
        {
            return embedding;
        }

        var normalized = new float[embedding.Length];
        for (var i = 0; i < embedding.Length; i++)
        {
            normalized[i] = embedding[i] / norm;
        }

        return normalized;
    }

    private static float CosineSimilarity(float[] left, float[] right)
    {
        if (left.Length == 0 || left.Length != right.Length)
        {
            return 0f;
        }

        var dot = 0f;
        for (var i = 0; i < left.Length; i++)
        {
            dot += left[i] * right[i];
        }

        return dot;
    }

    private static float NormalizeChannel(byte value)
    {
        return (value / 255f - 0.5f) / 0.5f;
    }

    private static int NormalizeDimension(int dimension, int fallback)
    {
        return dimension > 0 ? dimension : fallback;
    }

    private static string ResolveModelPath(IConfiguration configuration, IHostEnvironment environment)
    {
        var configuredPath = configuration["FaceRecognition:OnnxModelPath"];
        return ResolveExistingFilePath(environment, configuredPath, DefaultModelFileName, createIfMissing: true);
    }

    private static string ResolveEmbeddingDbPath(IConfiguration configuration, IHostEnvironment environment)
    {
        var configuredPath = configuration["FaceRecognition:EmbeddingDbPath"];
        return ResolveExistingFilePath(environment, configuredPath, DefaultEmbeddingDbFileName, createIfMissing: true);
    }

    private static string ResolveExistingFilePath(
        IHostEnvironment environment,
        string? configuredPath,
        string fileName,
        bool createIfMissing = false)
    {
        var candidates = new List<string>();

        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            candidates.Add(Path.IsPathRooted(configuredPath)
                ? configuredPath
                : Path.GetFullPath(Path.Combine(environment.ContentRootPath, configuredPath)));
        }

        candidates.Add(Path.Combine(environment.ContentRootPath, fileName));
        candidates.Add(Path.Combine(environment.ContentRootPath, "Models", fileName));

        var current = environment.ContentRootPath;
        for (var i = 0; i < 6; i++)
        {
            current = Path.GetFullPath(Path.Combine(current, ".."));
            candidates.Add(Path.Combine(current, fileName));
            candidates.Add(Path.Combine(current, "backend", "FaceRecognitionService", fileName));
        }

        var existingPath = candidates
            .Where(candidate => !string.IsNullOrWhiteSpace(candidate))
            .Select(Path.GetFullPath)
            .FirstOrDefault(File.Exists);

        if (!string.IsNullOrWhiteSpace(existingPath))
        {
            return existingPath;
        }

        return createIfMissing
            ? Path.GetFullPath(candidates.First())
            : string.Empty;
    }

    private sealed record StoredEmbedding(int StudentId, float[] Embedding);

    private sealed record ArcFaceModelContext(
        InferenceSession Session,
        string InputName,
        TensorLayout Layout,
        int Width,
        int Height);

    private enum TensorLayout
    {
        Nchw,
        Nhwc
    }
}
