using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using School.Domain.Entities;
using School.Infrastructure.Identity;

namespace School.Infrastructure.Data;

public class SchoolDbContextSeed
{
    private const string SystemOwnerAdminEmail = "mohammedzaghloul0123@gmail.com";
    private const string DefaultAdminPassword = "Admin@123";
    private const int MinimumTeacherCount = 10;
    private const int MinimumClassRoomCount = 24;
    private const int MinimumStudentCount = 1000;
    private const int TargetStudentsPerClass = 42;
    private static readonly (string Name, string Code, string Term)[] DemoSubjectCatalog =
    [
        ("اللغة العربية", "ARB", "الترم الأول"),
        ("الرياضيات", "MTH", "الترم الأول"),
        ("العلوم", "SCI", "الترم الأول"),
        ("اللغة الإنجليزية", "ENG", "الترم الثاني"),
        ("الدراسات الاجتماعية", "SOC", "الترم الثاني"),
        ("الحاسب الآلي", "ICT", "الترم الثاني")
    ];
    private static readonly string[] DemoTeacherNames =
    [
        "محمود عبدالله",
        "نهى شريف",
        "يوسف عادل",
        "هبة سمير",
        "طارق فؤاد",
        "ريم حسن",
        "عمرو نبيل",
        "سلمى سامح",
        "كريم ممدوح",
        "دينا خالد"
    ];

    private static readonly string[] DemoStudentFirstNames =
    [
        "أحمد", "محمد", "يوسف", "عمر", "مالك", "آدم", "كريم", "ياسين",
        "سليم", "زياد", "مروان", "حمزة", "ليلى", "ملك", "جنى", "نور",
        "سارة", "مريم", "رنا", "هنا", "تاليا", "حبيبة", "فريدة", "لارا"
    ];
    private static readonly string[] DemoStudentFamilyNames =
    [
        "محمود", "أحمد", "خالد", "حسن", "إبراهيم", "مصطفى", "عادل", "سامي",
        "عبدالله", "سمير", "فؤاد", "نبيل", "شريف", "صلاح", "منير", "طارق"
    ];
    private static readonly string[] DemoStudentMiddleNames =
    [
        "علي", "عامر", "هاني", "سعيد", "وائل", "رامي", "أيمن", "باسم",
        "كامل", "جمال", "ماجد", "وليد", "فاروق", "حسام", "ناصر", "رضا"
    ];

    public static async Task SeedAsync(SchoolDbContext context, UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager)
    {
        // 0. Ensure schema is updated (Safeguard for new columns)
        try {
            await context.Database.ExecuteSqlRawAsync("IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[Videos]') AND name = 'GradeLevelId') ALTER TABLE [Videos] ADD [GradeLevelId] int NULL;");
            await context.Database.ExecuteSqlRawAsync("IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[Videos]') AND name = 'IsHidden') ALTER TABLE [Videos] ADD [IsHidden] bit NOT NULL DEFAULT 0;");
            await context.Database.ExecuteSqlRawAsync("IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[Subjects]') AND name = 'Term') ALTER TABLE [Subjects] ADD [Term] nvarchar(100) NULL;");
            await context.Database.ExecuteSqlRawAsync("IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[Subjects]') AND name = 'IsActive') ALTER TABLE [Subjects] ADD [IsActive] bit NOT NULL DEFAULT 1;");
            await context.Database.ExecuteSqlRawAsync("IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[Subjects]') AND name = 'Term') UPDATE [Subjects] SET [Term] = N'الترم الأول' WHERE [Term] IS NULL;");
        } catch { /* Ignore if already exists or table not ready */ }

        var useCleanSeed = Environment.GetEnvironmentVariable("SCHOOL_USE_LEGACY_SEED") != "1";
        if (useCleanSeed)
        {
            await CleanSchoolSeed.SeedAsync(context, userManager, roleManager);
            return;
        }

        // 1. Seed Roles
        var roles = new List<string> { "Admin", "Teacher", "Student", "Parent" };
        foreach (var roleName in roles)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                await roleManager.CreateAsync(new IdentityRole(roleName));
            }
        }

        // 2. Seed Admin User
        var adminEmail = "admin@school.com";
        if (await userManager.FindByEmailAsync(adminEmail) == null)
        {
            var adminUser = new ApplicationUser
            {
                UserName = "admin",
                Email = adminEmail,
                FullName = "مدير النظام",
                EmailConfirmed = true
            };

            var result = await userManager.CreateAsync(adminUser, DefaultAdminPassword);
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(adminUser, "Admin");
            }
        }

        // 3. Seed Grade Levels
        if (!context.GradeLevels.Any())
        {
            var levels = new List<GradeLevel>
            {
                new GradeLevel { Name = "الصف الأول" },
                new GradeLevel { Name = "الصف الثاني" },
                new GradeLevel { Name = "الصف الثالث" }
            };
            context.GradeLevels.AddRange(levels);
            await context.SaveChangesAsync();
        }

        // 4. Seed Teachers
        if (!context.Teachers.Any())
        {
            var teacherData = new List<(string Name, string Email, string Phone)>
            {
                ("أحمد محروس", "ahmed@school.com", "0123456789"),
                ("سارة علي", "sara@school.com", "0987654321")
            };

            foreach (var t in teacherData)
            {
                var user = await userManager.FindByEmailAsync(t.Email);
                if (user == null)
                {
                    user = new ApplicationUser
                    {
                        UserName = t.Email,
                        Email = t.Email,
                        FullName = t.Name,
                        EmailConfirmed = true
                    };
                    var result = await userManager.CreateAsync(user, "Teacher@123");
                    if (result.Succeeded)
                    {
                        await userManager.AddToRoleAsync(user, "Teacher");
                    }
                }

                if (user != null && !context.Teachers.Any(teacher => teacher.UserId == user.Id))
                {
                    var teacher = new Teacher
                    {
                        UserId = user.Id,
                        FullName = t.Name,
                        Email = t.Email,
                        Phone = t.Phone,
                        IsActive = true
                    };
                    context.Teachers.Add(teacher);
                    await context.SaveChangesAsync();
                    user.TeacherId = teacher.Id;
                    await userManager.UpdateAsync(user);
                }
            }
        }

        // 5. Seed ClassRooms
        if (!context.ClassRooms.Any())
        {
            var levels = await context.GradeLevels.ToListAsync();
            var teacher1 = context.Teachers.First();
            var teacher2 = context.Teachers.Skip(1).FirstOrDefault() ?? teacher1;

            var classes = new List<ClassRoom>
            {
                new ClassRoom
                {
                    Name = "فصل 1/أ",
                    Capacity = 30,
                    Location = "الدور الأول",
                    AcademicYear = "2025/2026",
                    GradeLevelId = levels[0].Id,
                    TeacherId = teacher1.Id
                },
                new ClassRoom
                {
                    Name = "فصل 1/ب",
                    Capacity = 28,
                    Location = "الدور الأول",
                    AcademicYear = "2025/2026",
                    GradeLevelId = levels[0].Id,
                    TeacherId = teacher2.Id
                },
                new ClassRoom
                {
                    Name = "فصل 2/أ",
                    Capacity = 32,
                    Location = "الدور الثاني",
                    AcademicYear = "2025/2026",
                    GradeLevelId = levels[1].Id,
                    TeacherId = teacher1.Id
                }
            };
            context.ClassRooms.AddRange(classes);
            await context.SaveChangesAsync();
        }

        // 6. Seed Subjects
        if (!context.Subjects.Any())
        {
            var teacher1 = context.Teachers.First();
            var teacher2 = context.Teachers.Skip(1).FirstOrDefault() ?? teacher1;
            var class1 = context.ClassRooms.First();

            var subjects = new List<Subject>
            {
                new Subject { Name = "الرياضيات", Code = "MATH", Description = "مادة الرياضيات", TeacherId = teacher1.Id, ClassRoomId = class1.Id, Term = "الترم الأول", IsActive = true },
                new Subject { Name = "العلوم", Code = "SCI", Description = "مادة العلوم", TeacherId = teacher1.Id, ClassRoomId = class1.Id, Term = "الترم الأول", IsActive = true },
                new Subject { Name = "اللغة العربية", Code = "ARB", Description = "مادة اللغة العربية", TeacherId = teacher2.Id, ClassRoomId = class1.Id, Term = "الترم الأول", IsActive = true },
                new Subject { Name = "اللغة الإنجليزية", Code = "ENG", Description = "مادة اللغة الإنجليزية", TeacherId = teacher2.Id, ClassRoomId = class1.Id, Term = "الترم الثاني", IsActive = true },
                new Subject { Name = "التربية الإسلامية", Code = "ISL", Description = "مادة التربية الإسلامية", TeacherId = teacher1.Id, ClassRoomId = class1.Id, Term = "الترم الثاني", IsActive = true }
            };
            context.Subjects.AddRange(subjects);
            await context.SaveChangesAsync();
        }

        // 7. Seed Parent + link
        if (!context.Parents.Any())
        {
            var parentEmail = "parent@school.com";
            var parentUser = await userManager.FindByEmailAsync(parentEmail);
            if (parentUser == null)
            {
                parentUser = new ApplicationUser
                {
                    UserName = parentEmail,
                    Email = parentEmail,
                    FullName = "خالد أحمد",
                    EmailConfirmed = true
                };
                var result = await userManager.CreateAsync(parentUser, "Parent@123");
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(parentUser, "Parent");
                }
            }

            if (parentUser != null)
            {
                var parent = new Parent
                {
                    UserId = parentUser.Id,
                    FullName = "خالد أحمد",
                    Email = parentEmail,
                    Phone = "0112233445",
                    Address = "شارع التحرير، المعادي، القاهرة"
                };
                context.Parents.Add(parent);
                await context.SaveChangesAsync();

                parentUser.ParentId = parent.Id;
                await userManager.UpdateAsync(parentUser);
            }
        }

        // 8. Seed Students
        if (!context.Students.Any())
        {
            var class1 = context.ClassRooms.First();
            var class2 = context.ClassRooms.Skip(1).FirstOrDefault() ?? class1;
            var parent = context.Parents.FirstOrDefault();

            var studentData = new List<(string Name, string Email, int? ClassId, int? ParentId)>
            {
                ("محمد علي", "mohamed@school.com", class1.Id, parent?.Id),
                ("فاطمة حسن", "fatma@school.com", class1.Id, parent?.Id),
                ("عمر خالد", "omar@school.com", class1.Id, null),
                ("نورا سعيد", "noura@school.com", class2.Id, null)
            };

            foreach (var s in studentData)
            {
                var user = await userManager.FindByEmailAsync(s.Email);
                if (user == null)
                {
                    user = new ApplicationUser
                    {
                        UserName = s.Email,
                        Email = s.Email,
                        FullName = s.Name,
                        EmailConfirmed = true,
                        DeviceId = Guid.NewGuid().ToString()
                    };
                    var result = await userManager.CreateAsync(user, "Student@123");
                    if (result.Succeeded)
                    {
                        await userManager.AddToRoleAsync(user, "Student");
                    }
                }

                if (user != null && !context.Students.Any(student => student.UserId == user.Id))
                {
                    var student = new Student
                    {
                        UserId = user.Id,
                        FullName = s.Name,
                        Email = s.Email,
                        Phone = "0100000000",
                        ClassRoomId = s.ClassId,
                        ParentId = s.ParentId,
                        QrCodeValue = Guid.NewGuid().ToString()
                    };
                    context.Students.Add(student);
                    await context.SaveChangesAsync();
                    user.StudentId = student.Id;
                    await userManager.UpdateAsync(user);
                }
            }
        }

        await EnsureScaledAttendanceDemoDataAsync(context);
        await EnsureStudentSubjectAssignmentsAsync(context);
        await EnsureRequestedTestAccountsAsync(context, userManager);
        await EnsureScheduledSessionsAsync(context);
        await EnsureSampleWeeklySchedulesAsync(context);
        await EnsureCriticalWednesdaySessionsAsync(context);

        // 9. Seed Sessions (Last 14 days + Today + Upcoming)
        if (!context.Sessions.Any())
        {
            var teacher1 = context.Teachers.First();
            var class1 = context.ClassRooms.First();
            var class2 = context.ClassRooms.Skip(1).FirstOrDefault() ?? class1;
            var subjects = await context.Subjects.ToListAsync();
            var sessions = new List<Session>();

            for (int day = -14; day <= 2; day++)
            {
                var sessionDate = DateTime.Today.AddDays(day);
                // Simple 3 sessions per day for each of first two classes
                for (int sNum = 0; sNum < 3; sNum++)
                {
                    sessions.Add(new Session
                    {
                        Title = $"حصة {subjects[sNum % subjects.Count].Name}",
                        SessionDate = sessionDate,
                        StartTime = new TimeSpan(8 + sNum, 0, 0),
                        EndTime = new TimeSpan(8 + sNum, 45, 0),
                        AttendanceType = sNum == 0 ? "QR" : "Manual",
                        TeacherId = teacher1.Id,
                        ClassRoomId = (day % 2 == 0) ? class1.Id : class2.Id,
                        SubjectId = subjects[sNum % subjects.Count].Id
                    });
                }
            }
            context.Sessions.AddRange(sessions);
            await context.SaveChangesAsync();
        }

        await EnsureAttendanceRecordsAsync(context);

        // 10. Seed Attendance Records
        if (!context.Attendances.Any())
        {
            var students = await context.Students.ToListAsync();
            var sessions = await context.Sessions.ToListAsync();

            foreach (var session in sessions.Where(s => s.SessionDate <= DateTime.Today))
            {
                var classStudents = students.Where(s => s.ClassRoomId == session.ClassRoomId).ToList();
                foreach (var student in classStudents)
                {
                    var attendanceRoll = new Random((student.Id * 31) + (session.Id * 17)).Next(100);
                    var status = attendanceRoll switch
                    {
                        > 19 => "Present",
                        > 12 => "Late",
                        _ => "Absent"
                    };

                    var isPresent = status != "Absent";
                    var minutesAfterStart = status switch
                    {
                        "Late" => 12,
                        "Present" => 2,
                        _ => 0
                    };

                    context.Attendances.Add(new Attendance
                    {
                        StudentId = student.Id,
                        SessionId = session.Id,
                        IsPresent = isPresent,
                        Status = status,
                        Notes = status == "Late" ? "وصل بعد بداية الحصة بدقائق." : "",
                        Time = session.SessionDate.Add(session.StartTime).AddMinutes(minutesAfterStart),
                        RecordedAt = session.SessionDate.Add(session.StartTime),
                        Method = status == "Late" ? "Manual" : session.AttendanceType
                    });
                }
            }
            await context.SaveChangesAsync();
        }

        // 10.5. Seed Tuition Invoices
        if (!context.TuitionInvoices.Any())
        {
            var students = await context.Students
                .Include(s => s.ClassRoom)
                .ToListAsync();

            foreach (var student in students)
            {
                var academicYear = student.ClassRoom?.AcademicYear ?? "2025/2026";
                var baseAmount = 3200m + (student.Id % 3) * 350m;
                var serviceFee = 650m + (student.Id % 2) * 100m;
                var hasParentAccount = student.ParentId.HasValue;

                context.TuitionInvoices.Add(new TuitionInvoice
                {
                    StudentId = student.Id,
                    Title = "مصروفات الفصل الأول",
                    Description = $"المصروفات الأساسية للطالب {student.FullName}",
                    AcademicYear = academicYear,
                    Term = "الفصل الأول",
                    Amount = baseAmount,
                    AmountPaid = hasParentAccount ? baseAmount : Math.Round(baseAmount * 0.5m, 2),
                    DueDate = DateTime.Today.AddDays(-45),
                    PaidAt = hasParentAccount ? DateTime.Today.AddDays(-30) : null,
                    Status = hasParentAccount ? "Paid" : "Partial",
                    PaymentMethod = hasParentAccount ? "بطاقة" : "تحويل",
                    ReferenceNumber = hasParentAccount ? $"INV-{student.Id}-T1" : null,
                    Notes = "تم إنشاء الفاتورة تلقائيًا من بيانات المدرسة."
                });

                context.TuitionInvoices.Add(new TuitionInvoice
                {
                    StudentId = student.Id,
                    Title = "مصروفات الفصل الثاني",
                    Description = $"القسط الحالي للفصل الثاني للطالب {student.FullName}",
                    AcademicYear = academicYear,
                    Term = "الفصل الثاني",
                    Amount = baseAmount + 250m,
                    AmountPaid = hasParentAccount ? 1000m : 0m,
                    DueDate = DateTime.Today.AddDays(12 + student.Id),
                    Status = hasParentAccount ? "Partial" : "Pending",
                    Notes = "يمكن السداد إلكترونيًا من بوابة ولي الأمر."
                });

                context.TuitionInvoices.Add(new TuitionInvoice
                {
                    StudentId = student.Id,
                    Title = "رسوم الأنشطة والخدمات",
                    Description = "رسوم الأنشطة المدرسية والمواد الإضافية.",
                    AcademicYear = academicYear,
                    Term = "الخدمات",
                    Amount = serviceFee,
                    AmountPaid = 0m,
                    DueDate = DateTime.Today.AddDays(25 + student.Id),
                    Status = "Pending",
                    Notes = "تشمل الأنشطة والخدمات الرقمية."
                });
                context.TuitionInvoices.Add(new TuitionInvoice
                {
                    StudentId = student.Id,
                    Title = "رسوم التقييم الشهري",
                    Description = "رسوم اختبارات المتابعة الشهرية والتقارير الأكاديمية.",
                    AcademicYear = academicYear,
                    Term = "الخدمات",
                    Amount = 450m + (student.Id % 2) * 50m,
                    AmountPaid = 0m,
                    DueDate = DateTime.Today.AddDays(5 + student.Id),
                    Status = "Pending",
                    Notes = "فاتورة إضافية لعرض سيناريوهات متعددة في بوابة الدفع."
                });
            }

            await context.SaveChangesAsync();
        }

        // 11. Seed Assignments
        if (!context.Assignments.Any())
        {
            var teacher1 = context.Teachers.First();
            var class1 = context.ClassRooms.First();
            var subjects = await context.Subjects.ToListAsync();

            var assignments = new List<Assignment>
            {
                new Assignment
                {
                    Title = "حل تمارين الجبر - الفصل الثالث",
                    Description = "حل جميع تمارين الصفحات 45-50 من كتاب الرياضيات",
                    DueDate = DateTime.Today.AddDays(3),
                    TeacherId = teacher1.Id,
                    ClassRoomId = class1.Id,
                    SubjectId = subjects[0].Id
                },
                new Assignment
                {
                    Title = "تقرير عن التفاعلات الكيميائية",
                    Description = "كتابة تقرير من صفحتين عن أنواع التفاعلات الكيميائية مع أمثلة",
                    DueDate = DateTime.Today.AddDays(5),
                    TeacherId = teacher1.Id,
                    ClassRoomId = class1.Id,
                    SubjectId = subjects[1].Id
                },
                new Assignment
                {
                    Title = "إعراب الآيات القرآنية",
                    Description = "إعراب الآيات من سورة البقرة (آية 1-5) إعراباً تاماً",
                    DueDate = DateTime.Today.AddDays(2),
                    TeacherId = teacher1.Id,
                    ClassRoomId = class1.Id,
                    SubjectId = subjects[2].Id
                },
                new Assignment
                {
                    Title = "Write an English Essay",
                    Description = "Write a 200-word essay about your favorite hobby",
                    DueDate = DateTime.Today.AddDays(7),
                    TeacherId = teacher1.Id,
                    ClassRoomId = class1.Id,
                    SubjectId = subjects[3].Id
                }
            };
            context.Assignments.AddRange(assignments);
            await context.SaveChangesAsync();

            // Seed some submissions
            var assignmentsList = await context.Assignments.ToListAsync();
            var student1 = context.Students.First();
            context.AssignmentSubmissions.Add(new AssignmentSubmission
            {
                AssignmentId = assignmentsList[0].Id,
                StudentId = student1.Id,
                SubmissionDate = DateTime.UtcNow.AddHours(-2),
                FileUrl = "https://example.com/homework1.pdf",
                StudentNotes = "تم حل جميع التمارين",
                Grade = 90,
                TeacherFeedback = "عمل ممتاز!"
            });
            context.AssignmentSubmissions.Add(new AssignmentSubmission
            {
                AssignmentId = assignmentsList[1].Id,
                StudentId = student1.Id,
                SubmissionDate = DateTime.UtcNow.AddHours(-1),
                FileUrl = "https://example.com/report.pdf",
                StudentNotes = "التقرير جاهز"
            });
            await context.SaveChangesAsync();
        }

        // 12. Seed Grade Records
        if (!context.GradeRecords.Any())
        {
            var students = await context.Students.ToListAsync();
            var subjects = await context.Subjects.ToListAsync();
            var rng = new Random(42);

            foreach (var student in students)
            {
                foreach (var subject in subjects.Take(4))
                {
                    context.GradeRecords.Add(new GradeRecord
                    {
                        StudentId = student.Id,
                        SubjectId = subject.Id,
                        Score = rng.Next(60, 100),
                        GradeType = "Midterm",
                        Notes = "اختبار نصف الفصل",
                        Date = DateTime.Today.AddDays(-14)
                    });
                    context.GradeRecords.Add(new GradeRecord
                    {
                        StudentId = student.Id,
                        SubjectId = subject.Id,
                        Score = rng.Next(65, 100),
                        GradeType = "Homework",
                        Notes = "واجب منزلي",
                        Date = DateTime.Today.AddDays(-7)
                    });
                }
            }
            await context.SaveChangesAsync();
        }

        // 13. Seed Videos
        if (!context.Videos.Any())
        {
            var subjects = await context.Subjects.ToListAsync();
            var teacher1 = context.Teachers.First();

            var videos = new List<Video>
            {
                new Video
                {
                    Title = "شرح المعادلات من الدرجة الأولى",
                    Description = "شرح تفصيلي لحل المعادلات الخطية مع أمثلة",
                    Url = "https://www.youtube.com/watch?v=example1",
                    ThumbnailUrl = "https://img.youtube.com/vi/example1/hqdefault.jpg",
                    Duration = "15:30",
                    Views = 120,
                    SubjectId = subjects[0].Id,
                    TeacherId = teacher1.Id
                },
                new Video
                {
                    Title = "تجربة التفاعل الكيميائي",
                    Description = "فيديو عملي لتجربة التفاعلات الكيميائية في المختبر",
                    Url = "https://www.youtube.com/watch?v=example2",
                    ThumbnailUrl = "https://img.youtube.com/vi/example2/hqdefault.jpg",
                    Duration = "22:45",
                    Views = 85,
                    SubjectId = subjects[1].Id,
                    TeacherId = teacher1.Id
                },
                new Video
                {
                    Title = "قواعد النحو - المبتدأ والخبر",
                    Description = "شرح مبسط لقاعدة المبتدأ والخبر مع تمارين",
                    Url = "https://www.youtube.com/watch?v=example3",
                    ThumbnailUrl = "https://img.youtube.com/vi/example3/hqdefault.jpg",
                    Duration = "18:20",
                    Views = 200,
                    SubjectId = subjects[2].Id,
                    TeacherId = teacher1.Id
                },
                new Video
                {
                    Title = "English Grammar - Tenses",
                    Description = "Understanding present, past, and future tenses in English",
                    Url = "https://www.youtube.com/watch?v=example4",
                    ThumbnailUrl = "https://img.youtube.com/vi/example4/hqdefault.jpg",
                    Duration = "25:10",
                    Views = 150,
                    SubjectId = subjects[3].Id,
                    TeacherId = teacher1.Id
                }
            };
            context.Videos.AddRange(videos);
            await context.SaveChangesAsync();
        }

        // 14. Seed Exams
        if (!context.Exams.Any())
        {
            var teacher1 = context.Teachers.First();
            var class1 = context.ClassRooms.First();
            var subjects = await context.Subjects.ToListAsync();

            var exam1 = new Exam
            {
                Title = "اختبار الرياضيات - الفصل الأول",
                Description = "اختبار شامل على الجبر والهندسة والعمليات الحسابية الأساسية.",
                StartTime = DateTime.Today.AddDays(1).AddHours(8),
                EndTime = DateTime.Today.AddDays(1).AddHours(9).AddMinutes(30),
                MaxScore = 15,
                ExamType = "Midterm",
                TeacherId = teacher1.Id,
                ClassRoomId = class1.Id,
                SubjectId = subjects[0].Id,
                Questions = new List<Question>
                {
                    new Question { 
                        Text = "ما هو ناتج 5 + 7؟", 
                        Score = 5,
                        Choices = new List<QuestionChoice> {
                            new QuestionChoice { Text = "10", IsCorrect = false },
                            new QuestionChoice { Text = "11", IsCorrect = false },
                            new QuestionChoice { Text = "12", IsCorrect = true },
                            new QuestionChoice { Text = "13", IsCorrect = false }
                        }
                    },
                    new Question { 
                        Text = "ما هو ناتج 8 * 9؟", 
                        Score = 5,
                        Choices = new List<QuestionChoice> {
                            new QuestionChoice { Text = "64", IsCorrect = false },
                            new QuestionChoice { Text = "72", IsCorrect = true },
                            new QuestionChoice { Text = "81", IsCorrect = false },
                            new QuestionChoice { Text = "90", IsCorrect = false }
                        }
                    },
                    new Question { 
                        Text = "ما هي قيمة π التقريبية؟", 
                        Score = 5,
                        Choices = new List<QuestionChoice> {
                            new QuestionChoice { Text = "3.14", IsCorrect = true },
                            new QuestionChoice { Text = "2.14", IsCorrect = false },
                            new QuestionChoice { Text = "4.14", IsCorrect = false },
                            new QuestionChoice { Text = "1.14", IsCorrect = false }
                        }
                    }
                }
            };

            var exam2 = new Exam
            {
                Title = "اختبار قصير - العلوم",
                Description = "اختبار قصير على الوحدة الثالثة: الكائنات الحية والبيئة.",
                StartTime = DateTime.Today.AddDays(3).AddHours(10),
                EndTime = DateTime.Today.AddDays(3).AddHours(10).AddMinutes(30),
                MaxScore = 10,
                ExamType = "Quiz",
                TeacherId = teacher1.Id,
                ClassRoomId = class1.Id,
                SubjectId = subjects[1].Id,
                Questions = new List<Question>
                {
                    new Question { 
                        Text = "ما هو مصدر الطاقة الرئيسي للأرض؟", 
                        Score = 5,
                        Choices = new List<QuestionChoice> {
                            new QuestionChoice { Text = "القمر", IsCorrect = false },
                            new QuestionChoice { Text = "الشمس", IsCorrect = true },
                            new QuestionChoice { Text = "النجوم", IsCorrect = false },
                            new QuestionChoice { Text = "الرياح", IsCorrect = false }
                        }
                    },
                    new Question { 
                        Text = "أي غاز نحتاج للتنفس؟", 
                        Score = 5,
                        Choices = new List<QuestionChoice> {
                            new QuestionChoice { Text = "الأكسجين", IsCorrect = true },
                            new QuestionChoice { Text = "النيتروجين", IsCorrect = false },
                            new QuestionChoice { Text = "ثاني أكسيد الكربون", IsCorrect = false },
                            new QuestionChoice { Text = "الهيدروجين", IsCorrect = false }
                        }
                    }
                }
            };

            context.Exams.AddRange(new List<Exam> { exam1, exam2 });
            await context.SaveChangesAsync();
        }

        // 15. Seed Announcements
        if (!context.Announcements.Any())
        {
            var adminUser = await userManager.FindByEmailAsync("admin@school.com");
            var announcements = new List<Announcement>
            {
                new Announcement
                {
                    Title = "بداية الفصل الدراسي الثاني",
                    Content = "يسعدنا إعلامكم ببدء الفصل الدراسي الثاني يوم الأحد القادم. نتمنى للجميع فصلاً دراسياً موفقاً.",
                    CreatedAt = DateTime.UtcNow.AddDays(-3),
                    Audience = "All",
                    CreatedBy = adminUser?.Id ?? ""
                },
                new Announcement
                {
                    Title = "اجتماع أولياء الأمور",
                    Content = "ندعو جميع أولياء الأمور لحضور الاجتماع الدوري يوم الأربعاء القادم الساعة 4 مساءً في قاعة المدرسة.",
                    CreatedAt = DateTime.UtcNow.AddDays(-1),
                    Audience = "Parents",
                    CreatedBy = adminUser?.Id ?? ""
                },
                new Announcement
                {
                    Title = "تحديث جدول الاختبارات",
                    Content = "تم تحديث جدول الاختبارات النهائية. يرجى مراجعة الجدول الجديد من خلال لوحة التحكم.",
                    CreatedAt = DateTime.UtcNow,
                    Audience = "Students",
                    CreatedBy = adminUser?.Id ?? ""
                }
            };
            context.Announcements.AddRange(announcements);
            await context.SaveChangesAsync();
            // 17. Seed a few chat messages for Demo
            if (!context.Messages.Any())
            {
                var teacher = await context.Teachers.FirstOrDefaultAsync();
                var parent = await context.Parents.FirstOrDefaultAsync();
                if (teacher != null && parent != null)
                {
                    var messages = new List<Message>
                    {
                        new Message { SenderId = teacher.UserId, ReceiverId = parent.UserId, Content = "مرحباً أستاذ خالد، أردت فقط إبلاغك بأن أحمد أدى بشكل رائع في اختبار الرياضيات اليوم.", SentAt = DateTime.Now.AddDays(-1) },
                        new Message { SenderId = parent.UserId, ReceiverId = teacher.UserId, Content = "شكراً جزيلاً أستاذ أحمد، يسعدني سماع ذلك. هل هناك أي نقاط يحتاج للتركيز عليها؟", SentAt = DateTime.Now.AddHours(-5) },
                        new Message { SenderId = teacher.UserId, ReceiverId = parent.UserId, Content = "فقط مراجعة جدول الضرب بشكل دوري، وسيكون ممتازاً.", SentAt = DateTime.Now.AddHours(-1) }
                    };
                    context.Messages.AddRange(messages);
                    await context.SaveChangesAsync();
                }
            }
        }

        await BackfillTeacherAccountsAsync(context, userManager);
        await BackfillStudentAccountsAsync(context, userManager);
    }

    private static async Task EnsureScaledAttendanceDemoDataAsync(SchoolDbContext context)
    {
        var gradeLevels = await context.GradeLevels
            .OrderBy(level => level.Id)
            .ToListAsync();

        if (gradeLevels.Count == 0)
        {
            return;
        }

        var teachers = await EnsureScaledTeacherPoolAsync(context);
        var classRooms = await EnsureScaledClassRoomsAsync(context, gradeLevels, teachers);
        await EnsureScaledSubjectsAsync(context, classRooms, teachers);
        await EnsureScaledStudentsAsync(context, classRooms);
        await BackfillDemoStudentNamesAsync(context);
    }

    private static async Task<List<Teacher>> EnsureScaledTeacherPoolAsync(SchoolDbContext context)
    {
        var teachers = await context.Teachers
            .OrderBy(teacher => teacher.Id)
            .ToListAsync();

        if (teachers.Count >= MinimumTeacherCount)
        {
            return teachers;
        }

        var existingEmails = teachers
            .Select(teacher => NormalizeEmail(teacher.Email))
            .Where(email => !string.IsNullOrWhiteSpace(email))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var newTeachers = new List<Teacher>();
        var index = 0;

        while (teachers.Count + newTeachers.Count < MinimumTeacherCount)
        {
            var sequence = teachers.Count + newTeachers.Count + 1;
            var email = $"demo.teacher{sequence:00}@school.com";
            if (!existingEmails.Add(email))
            {
                continue;
            }

            var fullName = index < DemoTeacherNames.Length
                ? DemoTeacherNames[index]
                : $"مدرس تجريبي {sequence:00}";

            newTeachers.Add(new Teacher
            {
                FullName = fullName,
                Email = email,
                Phone = $"0107{sequence:000000}",
                IsActive = true
            });

            index++;
        }

        if (newTeachers.Count > 0)
        {
            context.Teachers.AddRange(newTeachers);
            await context.SaveChangesAsync();
        }

        return await context.Teachers
            .OrderBy(teacher => teacher.Id)
            .ToListAsync();
    }

    private static async Task<List<ClassRoom>> EnsureScaledClassRoomsAsync(
        SchoolDbContext context,
        IReadOnlyList<GradeLevel> gradeLevels,
        IReadOnlyList<Teacher> teachers)
    {
        var classRooms = await context.ClassRooms
            .OrderBy(classRoom => classRoom.Id)
            .ToListAsync();

        if (classRooms.Count >= MinimumClassRoomCount || teachers.Count == 0)
        {
            return classRooms;
        }

        var existingNames = classRooms
            .Select(classRoom => classRoom.Name ?? string.Empty)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var newClassRooms = new List<ClassRoom>();
        var sequence = classRooms.Count;

        while (classRooms.Count + newClassRooms.Count < MinimumClassRoomCount)
        {
            var teacherIndex = (sequence + newClassRooms.Count) % teachers.Count;
            var gradeIndex = (sequence + newClassRooms.Count) % gradeLevels.Count;
            var sectionNumber = ((sequence + newClassRooms.Count) / gradeLevels.Count) + 1;
            var name = $"فصل {gradeIndex + 1}/{sectionNumber:00}";

            if (!existingNames.Add(name))
            {
                sequence++;
                continue;
            }

            newClassRooms.Add(new ClassRoom
            {
                Name = name,
                Capacity = TargetStudentsPerClass,
                Location = $"الدور {(gradeIndex % 3) + 1}",
                AcademicYear = "2025/2026",
                GradeLevelId = gradeLevels[gradeIndex].Id,
                TeacherId = teachers[teacherIndex].Id
            });
        }

        if (newClassRooms.Count > 0)
        {
            context.ClassRooms.AddRange(newClassRooms);
            await context.SaveChangesAsync();
        }

        return await context.ClassRooms
            .OrderBy(classRoom => classRoom.Id)
            .ToListAsync();
    }

    private static async Task EnsureScaledSubjectsAsync(
        SchoolDbContext context,
        IReadOnlyList<ClassRoom> classRooms,
        IReadOnlyList<Teacher> teachers)
    {
        if (classRooms.Count == 0 || teachers.Count == 0)
        {
            return;
        }

        var existingSubjects = await context.Subjects
            .Where(subject => subject.ClassRoomId.HasValue)
            .ToListAsync();

        var existingKeys = existingSubjects
            .Select(subject => $"{subject.ClassRoomId}:{NormalizeValue(subject.Name)}")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var newSubjects = new List<Subject>();

        for (var classIndex = 0; classIndex < classRooms.Count; classIndex++)
        {
            var classRoom = classRooms[classIndex];

            for (var subjectIndex = 0; subjectIndex < DemoSubjectCatalog.Length; subjectIndex++)
            {
                var subjectTemplate = DemoSubjectCatalog[subjectIndex];
                var key = $"{classRoom.Id}:{NormalizeValue(subjectTemplate.Name)}";
                if (!existingKeys.Add(key))
                {
                    continue;
                }

                var teacher = teachers[(classIndex + subjectIndex) % teachers.Count];
                if (classIndex < 6 && subjectIndex < 2)
                {
                    teacher = teachers[0];
                }

                newSubjects.Add(new Subject
                {
                    Name = subjectTemplate.Name,
                    Code = $"{subjectTemplate.Code}-{classRoom.Id:000}",
                    Description = $"مقرر {subjectTemplate.Name} للفصل {classRoom.Name}",
                    TeacherId = teacher.Id,
                    ClassRoomId = classRoom.Id,
                    Term = subjectTemplate.Term,
                    IsActive = true
                });
            }
        }

        if (newSubjects.Count == 0)
        {
            return;
        }

        context.Subjects.AddRange(newSubjects);
        await context.SaveChangesAsync();
    }

    private static async Task EnsureScaledStudentsAsync(
        SchoolDbContext context,
        IReadOnlyList<ClassRoom> classRooms)
    {
        if (classRooms.Count == 0)
        {
            return;
        }

        var currentStudentCount = await context.Students.CountAsync();
        if (currentStudentCount >= MinimumStudentCount)
        {
            return;
        }

        var studentCountsByClass = await context.Students
            .Where(student => student.ClassRoomId.HasValue)
            .GroupBy(student => student.ClassRoomId!.Value)
            .Select(group => new { ClassRoomId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.ClassRoomId, item => item.Count);

        var newStudents = new List<Student>();
        var guard = 0;

        while (currentStudentCount + newStudents.Count < MinimumStudentCount && guard < MinimumStudentCount * 3)
        {
            foreach (var classRoom in classRooms)
            {
                guard++;

                if (currentStudentCount + newStudents.Count >= MinimumStudentCount)
                {
                    break;
                }

                var currentCount = studentCountsByClass.TryGetValue(classRoom.Id, out var count)
                    ? count
                    : 0;

                if (currentCount >= TargetStudentsPerClass)
                {
                    continue;
                }

                var sequenceInClass = currentCount + 1;
                var index = currentStudentCount + newStudents.Count + 1;
                newStudents.Add(new Student
                {
                    FullName = BuildDemoStudentName(sequenceInClass + (classRoom.Id * DemoStudentFirstNames.Length)),
                    Phone = $"0105{index:000000}",
                    BirthDate = DateTime.Today.AddYears(-(9 + (index % 5))).AddDays(-(index % 240)),
                    QrCodeValue = $"DEMO-QR-{index:0000}-{Guid.NewGuid():N}",
                    ClassRoomId = classRoom.Id,
                    IsActive = true
                });

                studentCountsByClass[classRoom.Id] = currentCount + 1;
            }
        }

        if (newStudents.Count == 0)
        {
            return;
        }

        context.Students.AddRange(newStudents);
        await context.SaveChangesAsync();
    }

    private static async Task BackfillDemoStudentNamesAsync(SchoolDbContext context)
    {
        var demoStudents = await context.Students
            .OrderBy(student => student.ClassRoomId)
            .ThenBy(student => student.Id)
            .ToListAsync();

        var sequenceByClassRoomId = new Dictionary<int, int>();
        var renamedCount = 0;
        foreach (var student in demoStudents)
        {
            if (!IsSeedGeneratedStudentName(student.FullName) &&
                !(string.IsNullOrWhiteSpace(student.Email) && IsDemoCatalogStudentName(student.FullName)))
            {
                continue;
            }

            var classRoomId = student.ClassRoomId ?? 0;
            sequenceByClassRoomId.TryGetValue(classRoomId, out var sequenceInClass);
            sequenceInClass++;
            sequenceByClassRoomId[classRoomId] = sequenceInClass;

            student.FullName = BuildDemoStudentName(sequenceInClass + (classRoomId * DemoStudentFirstNames.Length));
            renamedCount++;
        }

        if (renamedCount > 0)
        {
            await context.SaveChangesAsync();
        }
    }

    private static string BuildDemoStudentName(int index)
    {
        var firstName = DemoStudentFirstNames[(index - 1) % DemoStudentFirstNames.Length];
        var middleName = DemoStudentMiddleNames[((index - 1) / DemoStudentFirstNames.Length) % DemoStudentMiddleNames.Length];
        var familyName = DemoStudentFamilyNames[((index - 1) / (DemoStudentFirstNames.Length * DemoStudentMiddleNames.Length)) % DemoStudentFamilyNames.Length];
        return $"{firstName} {middleName} {familyName}";
    }

    private static bool IsSeedGeneratedStudentName(string? fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName))
        {
            return true;
        }

        var parts = fullName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return fullName.Contains("تجريبي") ||
            fullName.Contains("ØªØ¬Ø±ÙŠØ¨ÙŠ") ||
            fullName.StartsWith("طالب ") ||
            (parts.Length >= 3 && parts[^1].Length == 3 && parts[^1].All(char.IsDigit));
    }

    private static bool IsDemoCatalogStudentName(string? fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName))
        {
            return false;
        }

        var parts = fullName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 3 &&
            DemoStudentFirstNames.Contains(parts[0]) &&
            DemoStudentMiddleNames.Contains(parts[1]) &&
            DemoStudentFamilyNames.Contains(parts[2]);
    }

    private static async Task EnsureCriticalWednesdaySessionsAsync(SchoolDbContext context)
    {
        var teacher = await context.Teachers
            .OrderBy(currentTeacher => currentTeacher.Id)
            .FirstOrDefaultAsync(currentTeacher => currentTeacher.Email == "ahmed@school.com");

        teacher ??= await context.Teachers
            .OrderBy(currentTeacher => currentTeacher.Id)
            .FirstOrDefaultAsync();

        if (teacher == null)
        {
            return;
        }

        var teacherSubjects = await context.Subjects
            .Where(subject => subject.TeacherId == teacher.Id && subject.ClassRoomId.HasValue)
            .OrderBy(subject => subject.ClassRoomId)
            .ThenBy(subject => subject.Name)
            .ToListAsync();

        if (teacherSubjects.Count == 0)
        {
            return;
        }

        var targetDate = GetReferenceWednesday();
        var existingSessions = await context.Sessions
            .Where(session => session.SessionDate == targetDate)
            .ToListAsync();

        var slots = new[]
        {
            (Start: new TimeSpan(11, 15, 0), End: new TimeSpan(12, 0, 0), AttendanceType: "Face"),
            (Start: new TimeSpan(12, 15, 0), End: new TimeSpan(13, 0, 0), AttendanceType: "QR"),
            (Start: new TimeSpan(13, 15, 0), End: new TimeSpan(14, 0, 0), AttendanceType: "Manual")
        };

        var guaranteedSessions = new List<Session>();

        for (var index = 0; index < slots.Length; index++)
        {
            var subject = teacherSubjects[index % teacherSubjects.Count];
            var slot = slots[index];
            var classRoomId = subject.ClassRoomId!.Value;

            var exists = existingSessions.Any(session =>
                session.ClassRoomId == classRoomId &&
                session.SessionDate == targetDate &&
                session.StartTime == slot.Start);

            if (exists)
            {
                continue;
            }

            guaranteedSessions.Add(new Session
            {
                Title = $"حصة {subject.Name}",
                SessionDate = targetDate,
                StartTime = slot.Start,
                EndTime = slot.End,
                AttendanceType = slot.AttendanceType,
                TeacherId = teacher.Id,
                ClassRoomId = classRoomId,
                SubjectId = subject.Id
            });
        }

        if (guaranteedSessions.Count == 0)
        {
            return;
        }

        context.Sessions.AddRange(guaranteedSessions);
        await context.SaveChangesAsync();
    }

    private static async Task EnsureScheduledSessionsAsync(SchoolDbContext context)
    {
        var subjects = await context.Subjects
            .Where(subject => subject.IsActive && subject.TeacherId.HasValue && subject.ClassRoomId.HasValue)
            .ToListAsync();

        if (subjects.Count == 0)
        {
            return;
        }

        var referenceWednesday = GetReferenceWednesday();
        var startDate = DateTime.Today.AddDays(-7) <= referenceWednesday.AddDays(-3)
            ? DateTime.Today.AddDays(-7)
            : referenceWednesday.AddDays(-3);
        var endDate = DateTime.Today.AddDays(42) >= referenceWednesday.AddDays(2)
            ? DateTime.Today.AddDays(42)
            : referenceWednesday.AddDays(2);
        var existingSessions = await context.Sessions
            .Where(session => session.SessionDate >= startDate && session.SessionDate <= endDate)
            .ToListAsync();

        var generatedSessions = SessionScheduleGenerator.BuildMissingSessions(
            subjects,
            existingSessions,
            startDate,
            endDate);

        if (generatedSessions.Count == 0)
        {
            return;
        }

        context.Sessions.AddRange(generatedSessions);
        await context.SaveChangesAsync();
    }

    private static async Task EnsureStudentSubjectAssignmentsAsync(SchoolDbContext context)
    {
        var students = await context.Students
            .Where(student => student.ClassRoomId.HasValue)
            .Select(student => new { student.Id, student.ClassRoomId })
            .ToListAsync();

        if (students.Count == 0)
        {
            return;
        }

        var subjects = await context.Subjects
            .Where(subject => subject.IsActive && subject.ClassRoomId.HasValue)
            .Select(subject => new { subject.Id, subject.ClassRoomId })
            .ToListAsync();

        if (subjects.Count == 0)
        {
            return;
        }

        var existingKeys = await context.StudentSubjects
            .Select(item => new { item.StudentId, item.SubjectId })
            .ToListAsync();

        var keySet = existingKeys
            .Select(item => $"{item.StudentId}:{item.SubjectId}")
            .ToHashSet();

        var newAssignments = new List<StudentSubject>();

        foreach (var student in students)
        {
            var classroomSubjectIds = subjects
                .Where(subject => subject.ClassRoomId == student.ClassRoomId)
                .Select(subject => subject.Id)
                .Distinct()
                .ToList();

            foreach (var subjectId in classroomSubjectIds)
            {
                var key = $"{student.Id}:{subjectId}";
                if (keySet.Add(key))
                {
                    newAssignments.Add(new StudentSubject
                    {
                        StudentId = student.Id,
                        SubjectId = subjectId
                    });
                }
            }
        }

        if (newAssignments.Count == 0)
        {
            return;
        }

        context.StudentSubjects.AddRange(newAssignments);
        await context.SaveChangesAsync();
    }

    private static async Task EnsureSampleWeeklySchedulesAsync(SchoolDbContext context)
    {
        var existingSaturdaySchedule = await context.Schedules.AnyAsync(schedule => schedule.DayOfWeek == DayOfWeek.Saturday);
        if (existingSaturdaySchedule)
        {
            return;
        }

        var subject = await context.Subjects
            .Where(item => item.IsActive && item.TeacherId.HasValue && item.ClassRoomId.HasValue)
            .OrderBy(item => item.Id)
            .FirstOrDefaultAsync();

        if (subject == null)
        {
            return;
        }

        var startDate = DateTime.Today.Date.AddDays(-14);
        var endDate = DateTime.Today.Date.AddMonths(3);

        var schedule = new Schedule
        {
            Title = $"Weekly {subject.Name} Saturday Class",
            TeacherId = subject.TeacherId!.Value,
            SubjectId = subject.Id,
            ClassRoomId = subject.ClassRoomId!.Value,
            DayOfWeek = DayOfWeek.Saturday,
            StartTime = new TimeSpan(10, 0, 0),
            EndTime = new TimeSpan(11, 0, 0),
            AttendanceType = "Manual",
            TermStartDate = startDate,
            TermEndDate = endDate,
            IsActive = true
        };

        context.Schedules.Add(schedule);
        await context.SaveChangesAsync();

        var existingSessionDates = await context.Sessions
            .Where(session => session.ClassRoomId == schedule.ClassRoomId)
            .Where(session => session.SubjectId == schedule.SubjectId)
            .Where(session => session.SessionDate >= startDate && session.SessionDate <= endDate)
            .Select(session => new { session.SessionDate, session.StartTime, session.EndTime })
            .ToListAsync();

        var existingKeys = existingSessionDates
            .Select(item => $"{item.SessionDate:yyyyMMdd}:{item.StartTime:c}:{item.EndTime:c}")
            .ToHashSet();

        var generatedSessions = new List<Session>();

        for (var date = startDate; date <= endDate; date = date.AddDays(1))
        {
            if (date.DayOfWeek != schedule.DayOfWeek)
            {
                continue;
            }

            var key = $"{date:yyyyMMdd}:{schedule.StartTime:c}:{schedule.EndTime:c}";
            if (!existingKeys.Add(key))
            {
                continue;
            }

            generatedSessions.Add(new Session
            {
                Title = schedule.Title,
                SessionDate = date,
                StartTime = schedule.StartTime,
                EndTime = schedule.EndTime,
                AttendanceType = schedule.AttendanceType,
                TeacherId = schedule.TeacherId,
                ClassRoomId = schedule.ClassRoomId,
                SubjectId = schedule.SubjectId,
                ScheduleId = schedule.Id
            });
        }

        if (generatedSessions.Count > 0)
        {
            context.Sessions.AddRange(generatedSessions);
            schedule.SessionsGeneratedUntil = endDate;
            await context.SaveChangesAsync();
        }
    }

    private static async Task EnsureAttendanceRecordsAsync(SchoolDbContext context)
    {
        var referenceWednesday = GetReferenceWednesday();
        var today = DateTime.Today >= referenceWednesday.Date
            ? DateTime.Today
            : referenceWednesday.Date;
        var students = await context.Students.ToListAsync();
        var sessions = await context.Sessions
            .Where(session => session.SessionDate <= today)
            .ToListAsync();

        if (students.Count == 0 || sessions.Count == 0)
        {
            return;
        }

        var existingKeys = await context.Attendances
            .Select(attendance => new { attendance.StudentId, attendance.SessionId })
            .ToListAsync();

        var attendanceKeySet = existingKeys
            .Select(item => $"{item.StudentId}:{item.SessionId}")
            .ToHashSet();

        var newAttendances = new List<Attendance>();

        foreach (var session in sessions)
        {
            var classStudents = students.Where(student => student.ClassRoomId == session.ClassRoomId).ToList();
            foreach (var student in classStudents)
            {
                var key = $"{student.Id}:{session.Id}";
                if (attendanceKeySet.Contains(key))
                {
                    continue;
                }

                var attendanceRoll = new Random((student.Id * 31) + (session.Id * 17)).Next(100);
                var status = attendanceRoll switch
                {
                    > 19 => "Present",
                    > 12 => "Late",
                    _ => "Absent"
                };

                var isPresent = status != "Absent";
                var minutesAfterStart = status switch
                {
                    "Late" => 12,
                    "Present" => 2,
                    _ => 0
                };

                newAttendances.Add(new Attendance
                {
                    StudentId = student.Id,
                    SessionId = session.Id,
                    IsPresent = isPresent,
                    Status = status,
                    Notes = status == "Late" ? "وصل بعد بداية الحصة بدقائق." : string.Empty,
                    Time = session.SessionDate.Add(session.StartTime).AddMinutes(minutesAfterStart),
                    RecordedAt = session.SessionDate.Add(session.StartTime),
                    Method = status == "Late" ? "Manual" : session.AttendanceType
                });

                attendanceKeySet.Add(key);
            }
        }

        if (newAttendances.Count == 0)
        {
            return;
        }

        context.Attendances.AddRange(newAttendances);
        await context.SaveChangesAsync();
    }

    private static async Task EnsureRequestedTestAccountsAsync(SchoolDbContext context, UserManager<ApplicationUser> userManager)
    {
        var classRoom = await context.ClassRooms
            .OrderBy(currentClassRoom => currentClassRoom.Id)
            .FirstOrDefaultAsync();

        var adminUser = await EnsureSeedUserAsync(
            userManager,
            SystemOwnerAdminEmail,
            "محمد زغلول",
            "Admin",
            DefaultAdminPassword);

        var teacherUser = await EnsureSeedUserAsync(
            userManager,
            "mozaghloul0123@gmail.com",
            "أحمد محروس - اختبار Gmail",
            "Teacher",
            "Teacher@123");

        var parentUser = await EnsureSeedUserAsync(
            userManager,
            "mohammedzaghloul9000@gmail.com",
            "ولي أمر تجريبي",
            "Parent",
            "Parent@123");

        var firstStudentUser = await EnsureSeedUserAsync(
            userManager,
            "mohammedzaghloul8000@gmail.com",
            "يوسف محمد علي",
            "Student",
            "Student@123");

        var secondStudentUser = await EnsureSeedUserAsync(
            userManager,
            "mohammedzaghloul7000@gmail.com",
            "مريم أحمد حسن",
            "Student",
            "Student@123");

        if (adminUser.EmailConfirmed == false)
        {
            adminUser.EmailConfirmed = true;
            await userManager.UpdateAsync(adminUser);
        }

        var teacher = await context.Teachers
            .FirstOrDefaultAsync(currentTeacher =>
                currentTeacher.UserId == teacherUser.Id ||
                currentTeacher.Email == teacherUser.Email);

        if (teacher == null)
        {
            teacher = new Teacher
            {
                UserId = teacherUser.Id,
                FullName = teacherUser.FullName,
                Email = teacherUser.Email,
                Phone = "01001001001",
                IsActive = true
            };
            context.Teachers.Add(teacher);
            await context.SaveChangesAsync();
        }
        else
        {
            teacher.UserId = teacherUser.Id;
            teacher.FullName = teacherUser.FullName;
            teacher.Email = teacherUser.Email;
            teacher.IsActive = true;
            await context.SaveChangesAsync();
        }

        teacherUser.TeacherId = teacher.Id;
        await userManager.UpdateAsync(teacherUser);

        var parent = await context.Parents
            .FirstOrDefaultAsync(currentParent =>
                currentParent.UserId == parentUser.Id ||
                currentParent.Email == parentUser.Email);

        if (parent == null)
        {
            parent = new Parent
            {
                UserId = parentUser.Id,
                FullName = parentUser.FullName,
                Email = parentUser.Email ?? "mohammedzaghloul9000@gmail.com",
                Phone = "01009009000",
                Address = "حساب اختبار OTP"
            };
            context.Parents.Add(parent);
            await context.SaveChangesAsync();
        }
        else
        {
            parent.UserId = parentUser.Id;
            parent.FullName = parentUser.FullName;
            parent.Email = parentUser.Email ?? parent.Email;
            await context.SaveChangesAsync();
        }

        parentUser.ParentId = parent.Id;
        await userManager.UpdateAsync(parentUser);

        await EnsureSeedStudentAsync(context, userManager, firstStudentUser, parent.Id, classRoom?.Id, "01008008000");
        await EnsureSeedStudentAsync(context, userManager, secondStudentUser, parent.Id, classRoom?.Id, "01007007000");

        if (classRoom != null && !await context.Subjects.AnyAsync(subject => subject.TeacherId == teacher.Id))
        {
            context.Subjects.Add(new Subject
            {
                Name = "الرياضيات - اختبار Gmail",
                Code = $"GMATH-{teacher.Id}",
                Description = "مادة تجريبية لحسابات Gmail واختبارات الدرجات والحضور.",
                TeacherId = teacher.Id,
                ClassRoomId = classRoom.Id,
                Term = "الفصل الأول",
                IsActive = true
            });
            await context.SaveChangesAsync();
        }
    }

    private static async Task<ApplicationUser> EnsureSeedUserAsync(
        UserManager<ApplicationUser> userManager,
        string email,
        string fullName,
        string role,
        string password)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var user = await userManager.FindByEmailAsync(normalizedEmail);

        if (user == null)
        {
            user = new ApplicationUser
            {
                UserName = normalizedEmail,
                Email = normalizedEmail,
                FullName = fullName,
                EmailConfirmed = true,
                DeviceId = role == "Student" ? Guid.NewGuid().ToString() : null
            };

            var createResult = await userManager.CreateAsync(user, password);
            if (!createResult.Succeeded)
            {
                throw new InvalidOperationException($"Unable to create seed user {normalizedEmail}: {string.Join(", ", createResult.Errors.Select(error => error.Description))}");
            }
        }

        user.UserName = normalizedEmail;
        user.Email = normalizedEmail;
        user.FullName = fullName;
        user.EmailConfirmed = true;

        if (role != "Student")
        {
            user.StudentId = null;
            user.DeviceId = null;
        }

        if (role != "Teacher")
        {
            user.TeacherId = null;
        }

        if (role != "Parent")
        {
            user.ParentId = null;
        }

        if (role == "Student")
        {
            user.DeviceId ??= Guid.NewGuid().ToString();
        }

        var currentRoles = await userManager.GetRolesAsync(user);
        foreach (var currentRole in currentRoles.Where(currentRole => !string.Equals(currentRole, role, StringComparison.OrdinalIgnoreCase)))
        {
            await userManager.RemoveFromRoleAsync(user, currentRole);
        }

        if (!await userManager.IsInRoleAsync(user, role))
        {
            await userManager.AddToRoleAsync(user, role);
        }

        var resetToken = await userManager.GeneratePasswordResetTokenAsync(user);
        await userManager.ResetPasswordAsync(user, resetToken, password);

        await userManager.UpdateAsync(user);
        return user;
    }

    private static async Task EnsureSeedStudentAsync(
        SchoolDbContext context,
        UserManager<ApplicationUser> userManager,
        ApplicationUser studentUser,
        int parentId,
        int? classRoomId,
        string phone)
    {
        var student = await context.Students
            .FirstOrDefaultAsync(currentStudent =>
                currentStudent.UserId == studentUser.Id ||
                currentStudent.Email == studentUser.Email);

        if (student == null)
        {
            student = new Student
            {
                UserId = studentUser.Id,
                FullName = studentUser.FullName,
                Email = studentUser.Email,
                Phone = phone,
                ClassRoomId = classRoomId,
                ParentId = parentId,
                QrCodeValue = Guid.NewGuid().ToString(),
                IsActive = true
            };
            context.Students.Add(student);
            await context.SaveChangesAsync();
        }
        else
        {
            student.UserId = studentUser.Id;
            student.FullName = studentUser.FullName;
            student.Email = studentUser.Email;
            student.Phone = string.IsNullOrWhiteSpace(student.Phone) ? phone : student.Phone;
            student.ClassRoomId ??= classRoomId;
            student.ParentId = parentId;
            student.QrCodeValue ??= Guid.NewGuid().ToString();
            student.IsActive = true;
            await context.SaveChangesAsync();
        }

        studentUser.StudentId = student.Id;
        studentUser.DeviceId ??= Guid.NewGuid().ToString();
        await userManager.UpdateAsync(studentUser);
    }

    private static async Task BackfillTeacherAccountsAsync(SchoolDbContext context, UserManager<ApplicationUser> userManager)
    {
        var teachers = await context.Teachers.ToListAsync();

        foreach (var teacher in teachers)
        {
            var email = NormalizeEmail(teacher.Email);
            if (string.IsNullOrWhiteSpace(email))
            {
                continue;
            }

            ApplicationUser? user = null;

            if (!string.IsNullOrWhiteSpace(teacher.UserId))
            {
                user = await userManager.FindByIdAsync(teacher.UserId);
            }

            user ??= await userManager.FindByEmailAsync(email);

            if (user == null)
            {
                user = new ApplicationUser
                {
                    UserName = email,
                    Email = email,
                    FullName = teacher.FullName ?? "معلم",
                    PhoneNumber = teacher.Phone,
                    TeacherId = teacher.Id,
                    EmailConfirmed = true
                };

                var createResult = await userManager.CreateAsync(user, "Teacher@123");
                if (!createResult.Succeeded)
                {
                    continue;
                }
            }

            if (!await userManager.IsInRoleAsync(user, "Teacher"))
            {
                await userManager.AddToRoleAsync(user, "Teacher");
            }

            user.UserName = email;
            user.Email = email;
            user.FullName = teacher.FullName ?? user.FullName;
            user.PhoneNumber = string.IsNullOrWhiteSpace(teacher.Phone) ? user.PhoneNumber : teacher.Phone;
            user.TeacherId = teacher.Id;

            await userManager.UpdateAsync(user);

            if (teacher.UserId != user.Id)
            {
                teacher.UserId = user.Id;
            }
        }

        await context.SaveChangesAsync();
    }

    private static async Task BackfillStudentAccountsAsync(SchoolDbContext context, UserManager<ApplicationUser> userManager)
    {
        var students = await context.Students.ToListAsync();

        foreach (var student in students)
        {
            var email = NormalizeEmail(student.Email);
            if (string.IsNullOrWhiteSpace(email))
            {
                continue;
            }

            ApplicationUser? user = null;

            if (!string.IsNullOrWhiteSpace(student.UserId))
            {
                user = await userManager.FindByIdAsync(student.UserId);
            }

            user ??= await userManager.FindByEmailAsync(email);

            if (user == null)
            {
                user = new ApplicationUser
                {
                    UserName = email,
                    Email = email,
                    FullName = student.FullName ?? "طالب",
                    PhoneNumber = student.Phone,
                    StudentId = student.Id,
                    DeviceId = Guid.NewGuid().ToString(),
                    EmailConfirmed = true
                };

                var createResult = await userManager.CreateAsync(user, "Student@123");
                if (!createResult.Succeeded)
                {
                    continue;
                }
            }

            if (!await userManager.IsInRoleAsync(user, "Student"))
            {
                await userManager.AddToRoleAsync(user, "Student");
            }

            user.UserName = email;
            user.Email = email;
            user.FullName = student.FullName ?? user.FullName;
            user.PhoneNumber = string.IsNullOrWhiteSpace(student.Phone) ? user.PhoneNumber : student.Phone;
            user.StudentId = student.Id;
            user.DeviceId ??= Guid.NewGuid().ToString();

            await userManager.UpdateAsync(user);

            if (student.UserId != user.Id)
            {
                student.UserId = user.Id;
            }
        }

        await context.SaveChangesAsync();
    }

    private static string NormalizeValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    private static DateTime GetReferenceWednesday()
    {
        var today = DateTime.Today.Date;
        var offset = ((int)DayOfWeek.Wednesday - (int)today.DayOfWeek + 7) % 7;
        return today.AddDays(offset);
    }

    private static string? NormalizeEmail(string? email)
    {
        return string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToLowerInvariant();
    }
}
    
