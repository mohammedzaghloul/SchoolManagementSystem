using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using School.Domain.Entities;
using School.Infrastructure.Identity;

namespace School.Infrastructure.Data;

public class SchoolDbContextSeed
{
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

            var result = await userManager.CreateAsync(adminUser, "Admin@123");
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
                    var isPresent = new Random(student.Id + session.Id).Next(100) > 15; // ~85% attendance
                    context.Attendances.Add(new Attendance
                    {
                        StudentId = student.Id,
                        SessionId = session.Id,
                        IsPresent = isPresent,
                        Status = isPresent ? "Present" : "Absent",
                        Notes = "",
                        Time = session.SessionDate.Add(session.StartTime).AddMinutes(isPresent ? 2 : 0),
                        RecordedAt = session.SessionDate.Add(session.StartTime),
                        Method = session.AttendanceType
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

    private static string? NormalizeEmail(string? email)
    {
        return string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToLowerInvariant();
    }
}
    
