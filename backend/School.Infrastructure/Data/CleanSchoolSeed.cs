using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using School.Domain.Entities;
using School.Infrastructure.Identity;

namespace School.Infrastructure.Data;

public static class CleanSchoolSeed
{
    private const string AdminPassword = "Admin@123";
    private const string TeacherPassword = "Teacher@123";
    private const string ParentPassword = "Parent@123";
    private const string StudentPassword = "Student@123";
    private const int DesiredStudentCount = 144;
    private const int StudentsPerParent = 2;

    private static readonly TeacherSeed[] TeacherSeeds =
    [
        new("أحمد محروس", "ahmed.mahrous@school.com", "01001001001", "الرياضيات"),
        new("سارة علي", "sara.ali@school.com", "01001001002", "اللغة العربية"),
        new("محمود عبدالله", "mahmoud.abdallah@school.com", "01001001003", "العلوم"),
        new("نهى شريف", "noha.sherif@school.com", "01001001004", "اللغة الإنجليزية"),
        new("يوسف عادل", "youssef.adel@school.com", "01001001005", "الحاسب الآلي"),
        new("هبة سمير", "heba.samir@school.com", "01001001006", "الدراسات الاجتماعية"),
        new("طارق فؤاد", "tarek.fouad@school.com", "01001001007", "التربية الإسلامية"),
        new("ريم حسن", "reem.hassan@school.com", "01001001008", "الأنشطة")
    ];

    private static readonly SubjectTemplate[] SubjectTemplates =
    [
        new("الرياضيات", "MATH", "الترم الثاني", "ahmed.mahrous@school.com", "تدريبات الحساب والهندسة وحل المسائل"),
        new("اللغة العربية", "ARB", "الترم الثاني", "sara.ali@school.com", "قراءة ونحو وتعبير وإملاء"),
        new("العلوم", "SCI", "الترم الثاني", "mahmoud.abdallah@school.com", "تجارب ومفاهيم علمية مناسبة للمرحلة"),
        new("اللغة الإنجليزية", "ENG", "الترم الثاني", "noha.sherif@school.com", "محادثة وقواعد وقراءة"),
        new("الحاسب الآلي", "ICT", "الترم الثاني", "youssef.adel@school.com", "مهارات رقمية واستخدام آمن للتكنولوجيا"),
        new("الدراسات الاجتماعية", "SOC", "الترم الثاني", "heba.samir@school.com", "تاريخ وجغرافيا وأنشطة بحثية")
    ];

    private static readonly ParentSeed[] ParentSeeds =
    [
        new("خالد أحمد", "parent01@school.com", "01120000001", "مدينة نصر، القاهرة"),
        new("منى حسن", "parent02@school.com", "01120000002", "المعادي، القاهرة"),
        new("وليد محمود", "parent03@school.com", "01120000003", "الهرم، الجيزة"),
        new("دينا سمير", "parent04@school.com", "01120000004", "الشروق، القاهرة"),
        new("إبراهيم فؤاد", "parent05@school.com", "01120000005", "المقطم، القاهرة"),
        new("هالة مصطفى", "parent06@school.com", "01120000006", "الدقي، الجيزة"),
        new("شريف عادل", "parent07@school.com", "01120000007", "التجمع الخامس، القاهرة"),
        new("أمل صلاح", "parent08@school.com", "01120000008", "حدائق الأهرام، الجيزة"),
        new("حسام نبيل", "parent09@school.com", "01120000009", "العبور، القاهرة"),
        new("نجلاء سامي", "parent10@school.com", "01120000010", "الزمالك، القاهرة"),
        new("ماجد منير", "parent11@school.com", "01120000011", "المهندسين، الجيزة"),
        new("ريم فاروق", "parent12@school.com", "01120000012", "مصر الجديدة، القاهرة")
    ];

    private static readonly string[] FirstNames =
    [
        "آدم", "ليلى", "يوسف", "مريم", "مالك", "نور", "عمر", "سارة",
        "ياسين", "جنى", "سليم", "ملك", "زياد", "فريدة", "مروان", "هنا",
        "حمزة", "لارا", "كريم", "رنا", "علي", "تاليا", "نديم", "نوران"
    ];

    private static readonly string[] MiddleNames =
    [
        "أحمد", "محمود", "خالد", "حسن", "إبراهيم", "مصطفى",
        "عادل", "سامي", "عبدالله", "سمير", "فؤاد", "نبيل"
    ];

    private static readonly string[] FamilyNames =
    [
        "الشريف", "النجار", "المنياوي", "البدري", "العطار", "الهادي",
        "الصاوي", "القاضي", "المرسي", "الرفاعي", "الفاروقي", "الأنصاري"
    ];

    public static async Task SeedAsync(
        SchoolDbContext context,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager)
    {
        await EnsureRolesAsync(roleManager);
        await EnsureAdminAccountsAsync(userManager);

        var hasSchoolData = await context.GradeLevels.AnyAsync()
            || await context.Teachers.AnyAsync()
            || await context.ClassRooms.AnyAsync()
            || await context.Students.AnyAsync();

        if (hasSchoolData)
        {
            await EnsureParentChildPairingAsync(context, userManager);
            await EnsureStudentTrackingCoverageAsync(context);
            return;
        }

        var now = DateTime.Now;
        var today = DateTime.Today;
        var academicYear = today.Month >= 8
            ? $"{today.Year}/{today.Year + 1}"
            : $"{today.Year - 1}/{today.Year}";
        var termStart = new DateTime(today.Year, today.Month >= 8 ? 9 : 2, 1);
        var termEnd = today.Month >= 8
            ? new DateTime(today.Year + 1, 1, 31)
            : new DateTime(today.Year, 6, 30);

        var teachersByEmail = await SeedTeachersAsync(context, userManager);
        var parents = await SeedParentsAsync(context, userManager);
        var gradeLevels = await SeedGradeLevelsAsync(context);
        var classRooms = await SeedClassRoomsAsync(context, gradeLevels, teachersByEmail, academicYear);
        var students = await SeedStudentsAsync(context, userManager, classRooms, parents);
        var subjects = await SeedSubjectsAsync(context, classRooms, teachersByEmail);

        await SeedStudentSubjectsAsync(context, students, subjects);

        var schedules = await SeedSchedulesAsync(context, subjects, termStart, termEnd);
        var sessions = await SeedSessionsAsync(context, schedules, today);

        await SeedAttendanceAsync(context, students, sessions, now);
        await SeedGradeRecordsAsync(context, students, subjects, today);
        await SeedTuitionInvoicesAsync(context, students, academicYear, today);
        await SeedVideosAsync(context, subjects, today);
        await SeedAssignmentsAsync(context, students, subjects, today);
        await SeedExamsAsync(context, students, subjects, today);
        await SeedMessagesAsync(context, userManager);
        await SeedAnnouncementsAsync(context, userManager, today);
    }

    public static async Task BootstrapIdentityAsync(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager)
    {
        await EnsureRolesAsync(roleManager);
        await EnsureAdminAccountsAsync(userManager);
    }

    public static async Task ResetDataAsync(SchoolDbContext context)
    {
        var deleteTables = new[]
        {
            "Grades",
            "GradeUploads",
            "GradeSessions",
            "QuestionChoices",
            "Questions",
            "ExamResults",
            "Exams",
            "AssignmentSubmissions",
            "Assignments",
            "Attendances",
            "Sessions",
            "Schedules",
            "StudentSubjects",
            "GradeRecords",
            "TuitionInvoices",
            "Messages",
            "Videos",
            "Announcements",
            "Students",
            "Subjects",
            "ClassRooms",
            "Parents",
            "Teachers",
            "GradeLevels",
            "EmailOtps",
            "AspNetUserTokens",
            "AspNetUserLogins",
            "AspNetUserClaims",
            "AspNetUserRoles",
            "AspNetRoleClaims",
            "AspNetUsers",
            "AspNetRoles"
        };

        var identityTables = new[]
        {
            "Announcements",
            "Assignments",
            "Attendances",
            "ClassRooms",
            "EmailOtps",
            "Exams",
            "ExamResults",
            "GradeLevels",
            "Grades",
            "GradeUploads",
            "GradeSessions",
            "GradeRecords",
            "Messages",
            "Parents",
            "QuestionChoices",
            "Questions",
            "Schedules",
            "Sessions",
            "Students",
            "Subjects",
            "Teachers",
            "TuitionInvoices",
            "Videos"
        };

        await using var transaction = await context.Database.BeginTransactionAsync();

#pragma warning disable EF1002 // Table names come from fixed in-code allowlists above.
        foreach (var tableName in deleteTables)
        {
            await context.Database.ExecuteSqlRawAsync($"DELETE FROM [{tableName}];");
        }

        foreach (var tableName in identityTables)
        {
            await context.Database.ExecuteSqlRawAsync($"IF OBJECT_ID(N'[{tableName}]', N'U') IS NOT NULL DBCC CHECKIDENT (N'[{tableName}]', RESEED, 0);");
        }
#pragma warning restore EF1002

        await transaction.CommitAsync();
    }

    private static async Task EnsureRolesAsync(RoleManager<IdentityRole> roleManager)
    {
        foreach (var roleName in new[] { "Admin", "Teacher", "Student", "Parent" })
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                await roleManager.CreateAsync(new IdentityRole(roleName));
            }
        }
    }

    private static async Task EnsureAdminAccountsAsync(UserManager<ApplicationUser> userManager)
    {
        await EnsureUserAsync(userManager, "admin@school.com", "مدير النظام", AdminPassword, "Admin");
        await EnsureUserAsync(userManager, "mohammedzaghloul0123@gmail.com", "محمد زغلول", AdminPassword, "Admin");
    }

    private static async Task<Dictionary<string, Teacher>> SeedTeachersAsync(
        SchoolDbContext context,
        UserManager<ApplicationUser> userManager)
    {
        var teachersByEmail = new Dictionary<string, Teacher>(StringComparer.OrdinalIgnoreCase);

        foreach (var teacherSeed in TeacherSeeds)
        {
            var user = await EnsureUserAsync(userManager, teacherSeed.Email, teacherSeed.FullName, TeacherPassword, "Teacher");
            var teacher = new Teacher
            {
                UserId = user.Id,
                FullName = teacherSeed.FullName,
                Email = teacherSeed.Email,
                Phone = teacherSeed.Phone,
                IsActive = true
            };

            context.Teachers.Add(teacher);
            teachersByEmail[teacherSeed.Email] = teacher;
        }

        await context.SaveChangesAsync();

        foreach (var teacher in teachersByEmail.Values)
        {
            var user = await userManager.FindByEmailAsync(teacher.Email!);
            if (user == null)
            {
                continue;
            }

            user.TeacherId = teacher.Id;
            await userManager.UpdateAsync(user);
        }

        return teachersByEmail;
    }

    private static async Task<List<Parent>> SeedParentsAsync(
        SchoolDbContext context,
        UserManager<ApplicationUser> userManager)
    {
        var parents = new List<Parent>();

        foreach (var parentSeed in GenerateParentSeeds(DesiredStudentCount / StudentsPerParent))
        {
            var user = await EnsureUserAsync(userManager, parentSeed.Email, parentSeed.FullName, ParentPassword, "Parent");
            var parent = new Parent
            {
                UserId = user.Id,
                FullName = parentSeed.FullName,
                Email = parentSeed.Email,
                Phone = parentSeed.Phone,
                Address = parentSeed.Address
            };

            context.Parents.Add(parent);
            parents.Add(parent);
        }

        await context.SaveChangesAsync();

        foreach (var parent in parents)
        {
            var user = await userManager.FindByEmailAsync(parent.Email);
            if (user == null)
            {
                continue;
            }

            user.ParentId = parent.Id;
            await userManager.UpdateAsync(user);
        }

        return parents;
    }

    private static async Task<List<GradeLevel>> SeedGradeLevelsAsync(SchoolDbContext context)
    {
        var gradeLevels = new List<GradeLevel>
        {
            new() { Name = "الصف الأول", Description = "المرحلة الابتدائية - مستوى أول" },
            new() { Name = "الصف الثاني", Description = "المرحلة الابتدائية - مستوى ثاني" },
            new() { Name = "الصف الثالث", Description = "المرحلة الابتدائية - مستوى ثالث" }
        };

        context.GradeLevels.AddRange(gradeLevels);
        await context.SaveChangesAsync();
        return gradeLevels;
    }

    private static async Task<List<ClassRoom>> SeedClassRoomsAsync(
        SchoolDbContext context,
        List<GradeLevel> gradeLevels,
        Dictionary<string, Teacher> teachersByEmail,
        string academicYear)
    {
        var homeroomTeachers = teachersByEmail.Values.ToList();
        var classes = new List<ClassRoom>();

        for (var gradeIndex = 0; gradeIndex < gradeLevels.Count; gradeIndex++)
        {
            foreach (var section in new[] { "أ", "ب" })
            {
                var teacher = homeroomTeachers[(classes.Count + gradeIndex) % homeroomTeachers.Count];
                classes.Add(new ClassRoom
                {
                    Name = $"فصل {gradeIndex + 1}/{section}",
                    Capacity = 28,
                    Location = $"الدور {gradeIndex + 1} - قاعة {section}",
                    AcademicYear = academicYear,
                    GradeLevelId = gradeLevels[gradeIndex].Id,
                    TeacherId = teacher.Id
                });
            }
        }

        context.ClassRooms.AddRange(classes);
        await context.SaveChangesAsync();
        return classes;
    }

    private static async Task<List<Student>> SeedStudentsAsync(
        SchoolDbContext context,
        UserManager<ApplicationUser> userManager,
        List<ClassRoom> classRooms,
        List<Parent> parents)
    {
        var students = new List<Student>();
        var studentIndex = 0;

        foreach (var classRoom in classRooms)
        {
            for (var indexInClass = 0; indexInClass < 24; indexInClass++)
            {
                var parent = parents[studentIndex / StudentsPerParent];
                var fullName = BuildStudentName(studentIndex, parent);
                var email = $"student{studentIndex + 1:000}@school.com";
                var user = await EnsureUserAsync(
                    userManager,
                    email,
                    fullName,
                    StudentPassword,
                    "Student",
                    user => user.DeviceId = $"seed-device-{studentIndex + 1:000}");

                var student = new Student
                {
                    UserId = user.Id,
                    FullName = fullName,
                    Email = email,
                    Phone = $"01030{studentIndex + 1:00000}",
                    BirthDate = DateTime.Today.AddYears(-8 - (studentIndex % 4)).AddDays(studentIndex % 180),
                    ParentId = parent.Id,
                    ClassRoomId = classRoom.Id,
                    QrCodeValue = $"STD-{studentIndex + 1:000}-{Guid.NewGuid():N}",
                    IsActive = true
                };

                context.Students.Add(student);
                students.Add(student);
                studentIndex++;
            }
        }

        await context.SaveChangesAsync();

        foreach (var student in students)
        {
            var user = await userManager.FindByEmailAsync(student.Email!);
            if (user == null)
            {
                continue;
            }

            user.StudentId = student.Id;
            await userManager.UpdateAsync(user);
        }

        return students;
    }

    private static async Task<List<Subject>> SeedSubjectsAsync(
        SchoolDbContext context,
        List<ClassRoom> classRooms,
        Dictionary<string, Teacher> teachersByEmail)
    {
        var subjects = new List<Subject>();

        foreach (var classRoom in classRooms)
        {
            foreach (var template in SubjectTemplates)
            {
                var teacher = teachersByEmail[template.TeacherEmail];
                subjects.Add(new Subject
                {
                    Name = template.Name,
                    Code = $"{template.Code}-{classRoom.Name!.Replace("فصل ", string.Empty).Replace("/", string.Empty)}",
                    Description = template.Description,
                    Term = template.Term,
                    TeacherId = teacher.Id,
                    Teacher = teacher,
                    ClassRoomId = classRoom.Id,
                    ClassRoom = classRoom,
                    IsActive = true
                });
            }
        }

        context.Subjects.AddRange(subjects);
        await context.SaveChangesAsync();
        return subjects;
    }

    private static async Task SeedStudentSubjectsAsync(
        SchoolDbContext context,
        List<Student> students,
        List<Subject> subjects)
    {
        var studentSubjects = new List<StudentSubject>();

        foreach (var student in students)
        {
            foreach (var subject in subjects.Where(subject => subject.ClassRoomId == student.ClassRoomId))
            {
                studentSubjects.Add(new StudentSubject
                {
                    StudentId = student.Id,
                    SubjectId = subject.Id,
                    EnrolledOnUtc = DateTime.UtcNow.AddDays(-30)
                });
            }
        }

        context.StudentSubjects.AddRange(studentSubjects);
        await context.SaveChangesAsync();
    }

    private static async Task<List<Schedule>> SeedSchedulesAsync(
        SchoolDbContext context,
        List<Subject> subjects,
        DateTime termStart,
        DateTime termEnd)
    {
        var days = new[]
        {
            DayOfWeek.Saturday,
            DayOfWeek.Sunday,
            DayOfWeek.Monday,
            DayOfWeek.Tuesday,
            DayOfWeek.Wednesday,
            DayOfWeek.Thursday
        };
        var starts = new[]
        {
            new TimeSpan(8, 0, 0),
            new TimeSpan(9, 0, 0),
            new TimeSpan(10, 0, 0),
            new TimeSpan(11, 0, 0),
            new TimeSpan(12, 0, 0),
            new TimeSpan(13, 0, 0)
        };
        var attendanceTypes = new[] { "QR", "Manual", "Face" };
        var teacherBookings = new HashSet<string>();
        var classBookings = new HashSet<string>();
        var schedules = new List<Schedule>();

        foreach (var subject in subjects.OrderBy(subject => subject.ClassRoomId).ThenBy(subject => subject.Code))
        {
            var desiredWeeklyCount = subject.Name is "الرياضيات" or "اللغة العربية" ? 2 : 1;

            for (var occurrence = 0; occurrence < desiredWeeklyCount; occurrence++)
            {
                var schedule = BuildAvailableSchedule(
                    subject,
                    occurrence,
                    days,
                    starts,
                    attendanceTypes,
                    teacherBookings,
                    classBookings,
                    termStart,
                    termEnd);

                schedules.Add(schedule);
            }
        }

        context.Schedules.AddRange(schedules);
        await context.SaveChangesAsync();
        return schedules;
    }

    private static Schedule BuildAvailableSchedule(
        Subject subject,
        int occurrence,
        DayOfWeek[] days,
        TimeSpan[] starts,
        string[] attendanceTypes,
        HashSet<string> teacherBookings,
        HashSet<string> classBookings,
        DateTime termStart,
        DateTime termEnd)
    {
        var seed = ((subject.ClassRoomId ?? 0) * 7) + (subject.TeacherId ?? 0) + occurrence;

        for (var offset = 0; offset < days.Length * starts.Length; offset++)
        {
            var day = days[(seed + offset) % days.Length];
            var start = starts[(seed + offset + occurrence) % starts.Length];
            var teacherKey = $"{subject.TeacherId}:{day}:{start}";
            var classKey = $"{subject.ClassRoomId}:{day}:{start}";

            if (teacherBookings.Contains(teacherKey) || classBookings.Contains(classKey))
            {
                continue;
            }

            teacherBookings.Add(teacherKey);
            classBookings.Add(classKey);

            return new Schedule
            {
                Title = $"{subject.Name} - {subject.ClassRoom?.Name ?? "الفصل"}",
                DayOfWeek = day,
                StartTime = start,
                EndTime = start.Add(TimeSpan.FromMinutes(45)),
                AttendanceType = attendanceTypes[(seed + occurrence) % attendanceTypes.Length],
                TermStartDate = termStart,
                TermEndDate = termEnd,
                TeacherId = subject.TeacherId!.Value,
                SubjectId = subject.Id,
                ClassRoomId = subject.ClassRoomId!.Value,
                IsActive = true
            };
        }

        var fallbackStart = starts[occurrence % starts.Length];
        return new Schedule
        {
            Title = $"{subject.Name} - {subject.ClassRoom?.Name ?? "الفصل"}",
            DayOfWeek = days[occurrence % days.Length],
            StartTime = fallbackStart,
            EndTime = fallbackStart.Add(TimeSpan.FromMinutes(45)),
            AttendanceType = "Manual",
            TermStartDate = termStart,
            TermEndDate = termEnd,
            TeacherId = subject.TeacherId!.Value,
            SubjectId = subject.Id,
            ClassRoomId = subject.ClassRoomId!.Value,
            IsActive = true
        };
    }

    private static async Task<List<Session>> SeedSessionsAsync(
        SchoolDbContext context,
        List<Schedule> schedules,
        DateTime today)
    {
        var currentWeekStart = GetSchoolWeekStart(today);
        var firstSessionDate = currentWeekStart.AddDays(-7);
        var lastSessionDate = currentWeekStart.AddDays(6);
        var sessions = new List<Session>();

        foreach (var schedule in schedules)
        {
            for (var date = firstSessionDate; date <= lastSessionDate; date = date.AddDays(1))
            {
                if (date.DayOfWeek != schedule.DayOfWeek)
                {
                    continue;
                }

                sessions.Add(new Session
                {
                    Title = schedule.Title,
                    SessionDate = date,
                    StartTime = schedule.StartTime,
                    EndTime = schedule.EndTime,
                    AttendanceType = schedule.AttendanceType,
                    TeacherId = schedule.TeacherId,
                    SubjectId = schedule.SubjectId,
                    ClassRoomId = schedule.ClassRoomId,
                    ScheduleId = schedule.Id,
                    IsLive = false
                });
            }

            schedule.SessionsGeneratedUntil = lastSessionDate;
        }

        context.Sessions.AddRange(sessions);
        await context.SaveChangesAsync();
        return sessions;
    }

    private static async Task SeedAttendanceAsync(
        SchoolDbContext context,
        List<Student> students,
        List<Session> sessions,
        DateTime now)
    {
        var attendanceRecords = new List<Attendance>();

        foreach (var session in sessions.Where(session => HasSessionEnded(session, now)))
        {
            var classStudents = students
                .Where(student => student.ClassRoomId == session.ClassRoomId && student.IsActive)
                .ToList();

            foreach (var student in classStudents)
            {
                var roll = Math.Abs(HashCode.Combine(student.Id, session.Id)) % 100;
                var status = roll switch
                {
                    < 8 => "Absent",
                    < 15 => "Late",
                    _ => "Present"
                };
                var recordedTime = session.SessionDate.Date
                    .Add(session.StartTime)
                    .AddMinutes(status == "Late" ? 12 : 3);

                attendanceRecords.Add(new Attendance
                {
                    StudentId = student.Id,
                    SessionId = session.Id,
                    IsPresent = status != "Absent",
                    Status = status,
                    Notes = status == "Absent" ? "غياب بدون عذر مسجل" : null,
                    Time = recordedTime,
                    RecordedAt = recordedTime.AddMinutes(5),
                    Method = session.AttendanceType
                });
            }
        }

        context.Attendances.AddRange(attendanceRecords);
        await context.SaveChangesAsync();
    }

    private static async Task SeedGradeRecordsAsync(
        SchoolDbContext context,
        List<Student> students,
        List<Subject> subjects,
        DateTime today)
    {
        var records = new List<GradeRecord>();
        var gradeTypes = new[] { "واجب", "اختبار قصير", "مشاركة" };

        foreach (var student in students.Where(student => student.IsActive))
        {
            foreach (var subject in subjects.Where(subject => subject.ClassRoomId == student.ClassRoomId))
            {
                for (var index = 0; index < gradeTypes.Length; index++)
                {
                    var scoreSeed = Math.Abs(HashCode.Combine(student.Id, subject.Id, index)) % 31;
                    records.Add(new GradeRecord
                    {
                        StudentId = student.Id,
                        SubjectId = subject.Id,
                        GradeType = gradeTypes[index],
                        Score = 69 + scoreSeed,
                        Date = today.AddDays(-14 + (index * 5)),
                        Notes = index == 0 ? "تم الحل في الموعد" : "رصد تجريبي منظم"
                    });
                }
            }
        }

        context.GradeRecords.AddRange(records);
        await context.SaveChangesAsync();
    }

    private static async Task SeedTuitionInvoicesAsync(
        SchoolDbContext context,
        List<Student> students,
        string academicYear,
        DateTime today)
    {
        var invoices = new List<TuitionInvoice>();

        foreach (var student in students)
        {
            var isPaid = student.Id % 3 == 0;
            invoices.Add(new TuitionInvoice
            {
                StudentId = student.Id,
                Title = "مصروفات الفصل الدراسي الثاني",
                Description = "القسط الأساسي للترم الثاني",
                AcademicYear = academicYear,
                Term = "الترم الثاني",
                Amount = 3150,
                AmountPaid = isPaid ? 3150 : 0,
                DueDate = today.AddDays(student.Id % 5 == 0 ? -3 : 14),
                CreatedAt = today.AddDays(-20),
                PaidAt = isPaid ? today.AddDays(-2) : null,
                Status = isPaid ? "Paid" : student.Id % 5 == 0 ? "Overdue" : "Pending",
                PaymentMethod = isPaid ? (student.Id % 2 == 0 ? "Vodafone Cash" : "Instapay") : null,
                ReferenceNumber = isPaid ? $"PAY-{student.Id:0000}-{today:MMdd}" : null,
                Notes = isPaid ? "تمت التسوية" : "في انتظار السداد"
            });

            invoices.Add(new TuitionInvoice
            {
                StudentId = student.Id,
                Title = "رسوم الأنشطة والخدمات",
                Description = "أنشطة مدرسية وخدمات رقمية",
                AcademicYear = academicYear,
                Term = "الترم الثاني",
                Amount = 750,
                AmountPaid = student.Id % 4 == 0 ? 750 : 0,
                DueDate = today.AddDays(21),
                CreatedAt = today.AddDays(-10),
                PaidAt = student.Id % 4 == 0 ? today.AddDays(-1) : null,
                Status = student.Id % 4 == 0 ? "Paid" : "Pending",
                PaymentMethod = student.Id % 4 == 0 ? "Fawry" : null,
                ReferenceNumber = student.Id % 4 == 0 ? $"FW-{student.Id:0000}" : null
            });
        }

        context.TuitionInvoices.AddRange(invoices);
        await context.SaveChangesAsync();
    }

    private static async Task SeedVideosAsync(
        SchoolDbContext context,
        List<Subject> subjects,
        DateTime today)
    {
        var videos = subjects.Select((subject, index) => new Video
        {
            Title = $"شرح {subject.Name} - {subject.ClassRoom?.Name}",
            Description = $"مراجعة قصيرة لموضوعات {subject.Name} مع تدريبات تطبيقية.",
            Url = $"https://example.com/videos/{subject.Code?.ToLowerInvariant()}",
            ThumbnailUrl = $"https://example.com/thumbnails/{subject.Code?.ToLowerInvariant()}.jpg",
            Duration = $"{12 + (index % 18)} دقيقة",
            Views = index % 4 == 0 ? 0 : 8 + (index * 3),
            CreatedAt = today.AddDays(-index % 20),
            SubjectId = subject.Id,
            TeacherId = subject.TeacherId,
            GradeLevelId = subject.ClassRoom?.GradeLevelId,
            IsHidden = index % 17 == 0
        }).ToList();

        context.Videos.AddRange(videos);
        await context.SaveChangesAsync();
    }

    private static async Task SeedAssignmentsAsync(
        SchoolDbContext context,
        List<Student> students,
        List<Subject> subjects,
        DateTime today)
    {
        var assignments = subjects
            .Where(subject => subject.Name is "اللغة العربية" or "الرياضيات" or "الحاسب الآلي")
            .Select((subject, index) => new Assignment
            {
                Title = subject.Name == "اللغة العربية" ? "إعراب الآيات القرآنية" : $"واجب {subject.Name} الأسبوعي",
                Description = $"حل تدريبات {subject.Name} وتسليم ملف أو صورة واضحة.",
                DueDate = today.AddDays(3 + (index % 4)),
                TeacherId = subject.TeacherId!.Value,
                ClassRoomId = subject.ClassRoomId!.Value,
                SubjectId = subject.Id,
                AttachmentUrl = null
            })
            .ToList();

        context.Assignments.AddRange(assignments);
        await context.SaveChangesAsync();

        var submissions = new List<AssignmentSubmission>();
        foreach (var assignment in assignments)
        {
            var classStudents = students
                .Where(student => student.ClassRoomId == assignment.ClassRoomId)
                .Take(8)
                .ToList();

            foreach (var student in classStudents)
            {
                submissions.Add(new AssignmentSubmission
                {
                    AssignmentId = assignment.Id,
                    StudentId = student.Id,
                    SubmissionDate = today.AddDays(-1).AddHours(18),
                    FileUrl = $"https://example.com/submissions/{assignment.Id}/{student.Id}.png",
                    StudentNotes = "تم رفع ملف الحل",
                    Grade = student.Id % 3 == 0 ? null : 75 + (student.Id % 20),
                    TeacherFeedback = student.Id % 3 == 0 ? null : "مراجعة جيدة"
                });
            }
        }

        context.AssignmentSubmissions.AddRange(submissions);
        await context.SaveChangesAsync();
    }

    private static async Task SeedExamsAsync(
        SchoolDbContext context,
        List<Student> students,
        List<Subject> subjects,
        DateTime today)
    {
        var exams = subjects
            .Where(subject => subject.Name is "الرياضيات" or "العلوم" or "اللغة الإنجليزية")
            .Take(12)
            .Select((subject, index) => new Exam
            {
                Title = $"اختبار {subject.Name} القصير",
                Description = "اختبار قصير على الدرس الأخير",
                StartTime = today.AddDays(index % 2 == 0 ? 2 : -5).AddHours(10),
                EndTime = today.AddDays(index % 2 == 0 ? 2 : -5).AddHours(10).AddMinutes(35),
                MaxScore = 20,
                ExamType = index % 2 == 0 ? "Quiz" : "Midterm",
                TeacherId = subject.TeacherId!.Value,
                ClassRoomId = subject.ClassRoomId!.Value,
                SubjectId = subject.Id
            })
            .ToList();

        context.Exams.AddRange(exams);
        await context.SaveChangesAsync();

        var questions = new List<Question>();
        foreach (var exam in exams)
        {
            questions.Add(new Question
            {
                ExamId = exam.Id,
                Text = $"اختر الإجابة الصحيحة في {exam.Title}",
                Score = 10
            });
            questions.Add(new Question
            {
                ExamId = exam.Id,
                Text = "سؤال تطبيقي قصير",
                Score = 10
            });
        }

        context.Questions.AddRange(questions);
        await context.SaveChangesAsync();

        var choices = new List<QuestionChoice>();
        foreach (var question in questions)
        {
            choices.Add(new QuestionChoice { QuestionId = question.Id, Text = "الإجابة الأولى", IsCorrect = true });
            choices.Add(new QuestionChoice { QuestionId = question.Id, Text = "الإجابة الثانية", IsCorrect = false });
        }

        context.QuestionChoices.AddRange(choices);

        var results = new List<ExamResult>();
        foreach (var exam in exams.Where(exam => exam.EndTime < DateTime.Now))
        {
            foreach (var student in students.Where(student => student.ClassRoomId == exam.ClassRoomId).Take(12))
            {
                results.Add(new ExamResult
                {
                    ExamId = exam.Id,
                    StudentId = student.Id,
                    Score = 12 + (student.Id % 9),
                    Notes = "نتيجة اختبار تجريبي",
                    CreatedAt = exam.EndTime.AddHours(2)
                });
            }
        }

        context.ExamResults.AddRange(results);
        await context.SaveChangesAsync();
    }

    private static async Task SeedMessagesAsync(
        SchoolDbContext context,
        UserManager<ApplicationUser> userManager)
    {
        var admin = await userManager.FindByEmailAsync("admin@school.com");
        var teacher = await userManager.FindByEmailAsync("ahmed.mahrous@school.com");
        var parent = await userManager.FindByEmailAsync("parent01@school.com");
        var student = await userManager.FindByEmailAsync("student001@school.com");

        if (admin == null || teacher == null || parent == null || student == null)
        {
            return;
        }

        context.Messages.AddRange(
            new Message
            {
                SenderId = admin.Id,
                ReceiverId = teacher.Id,
                Content = "تم تحديث جدول الأسبوع، برجاء مراجعة حصص الرياضيات.",
                SentAt = DateTime.Now.AddDays(-2).AddHours(9),
                IsRead = true,
                MessageType = "text"
            },
            new Message
            {
                SenderId = teacher.Id,
                ReceiverId = parent.Id,
                Content = "تم رفع واجب الرياضيات لهذا الأسبوع.",
                SentAt = DateTime.Now.AddDays(-1).AddHours(13),
                IsRead = false,
                MessageType = "text"
            },
            new Message
            {
                SenderId = student.Id,
                ReceiverId = teacher.Id,
                Content = "تم تسليم الواجب، هل يمكن مراجعة الملف؟",
                SentAt = DateTime.Now.AddHours(-6),
                IsRead = false,
                FileUrl = "https://example.com/submissions/sample.png",
                FileName = "حل الواجب.png",
                FileType = "image",
                FileSize = 245760,
                MessageType = "image"
            });

        await context.SaveChangesAsync();
    }

    private static async Task SeedAnnouncementsAsync(
        SchoolDbContext context,
        UserManager<ApplicationUser> userManager,
        DateTime today)
    {
        var admin = await userManager.FindByEmailAsync("admin@school.com");
        if (admin == null)
        {
            return;
        }

        context.Announcements.AddRange(
            new Announcement
            {
                Title = "تنبيه بخصوص الحضور",
                Content = "يتم رصد الحضور من داخل نافذة الحصة فقط، ويمكن للأدمن التعديل عند الحاجة.",
                Audience = "Teachers",
                CreatedBy = admin.Id,
                CreatedAt = today.AddDays(-1)
            },
            new Announcement
            {
                Title = "طرق الدفع المتاحة",
                Content = "يمكن تسجيل مدفوعات Vodafone Cash وInstapay وFawry من لوحة ولي الأمر.",
                Audience = "Parents",
                CreatedBy = admin.Id,
                CreatedAt = today.AddDays(-2)
            },
            new Announcement
            {
                Title = "متابعة الدرجات",
                Content = "تم تحديث سجل درجات الطلاب ليظهر آخر الواجبات والاختبارات.",
                Audience = "All",
                CreatedBy = admin.Id,
                CreatedAt = today.AddDays(-3)
            });

        await context.SaveChangesAsync();
    }

    private static async Task<ApplicationUser> EnsureUserAsync(
        UserManager<ApplicationUser> userManager,
        string email,
        string fullName,
        string password,
        string role,
        Action<ApplicationUser>? configure = null)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user == null)
        {
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                FullName = fullName,
                EmailConfirmed = true
            };
            configure?.Invoke(user);

            var result = await userManager.CreateAsync(user, password);
            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(error => error.Description));
                throw new InvalidOperationException($"Could not create seed user {email}: {errors}");
            }
        }
        else
        {
            user.FullName = fullName;
            configure?.Invoke(user);
            await userManager.UpdateAsync(user);
        }

        if (!await userManager.IsInRoleAsync(user, role))
        {
            await userManager.AddToRoleAsync(user, role);
        }

        return user;
    }

    private static IEnumerable<ParentSeed> GenerateParentSeeds(int desiredCount)
    {
        for (var index = 0; index < desiredCount; index++)
        {
            var baseParent = ParentSeeds[index % ParentSeeds.Length];
            var familyIndex = index / ParentSeeds.Length;
            var fullName = familyIndex == 0
                ? baseParent.FullName
                : $"{baseParent.FullName} {FamilyNames[familyIndex % FamilyNames.Length]}";

            yield return new ParentSeed(
                fullName,
                $"parent{index + 1:00}@school.com",
                $"01120{index + 1:000000}",
                baseParent.Address);
        }
    }

    private static async Task EnsureParentChildPairingAsync(
        SchoolDbContext context,
        UserManager<ApplicationUser> userManager)
    {
        var students = await context.Students
            .OrderBy(student => student.Id)
            .ToListAsync();

        if (students.Count == 0)
        {
            return;
        }

        var requiredParentCount = (int)Math.Ceiling(students.Count / (double)StudentsPerParent);
        var existingParents = await context.Parents
            .OrderBy(parent => parent.Id)
            .ToListAsync();
        var generatedParents = GenerateParentSeeds(requiredParentCount).ToList();

        for (var index = existingParents.Count; index < requiredParentCount; index++)
        {
            var parentSeed = generatedParents[index];
            var user = await EnsureUserAsync(userManager, parentSeed.Email, parentSeed.FullName, ParentPassword, "Parent");
            var parent = new Parent
            {
                UserId = user.Id,
                FullName = parentSeed.FullName,
                Email = parentSeed.Email,
                Phone = parentSeed.Phone,
                Address = parentSeed.Address
            };

            context.Parents.Add(parent);
            existingParents.Add(parent);
        }

        await context.SaveChangesAsync();

        for (var index = 0; index < existingParents.Count && index < generatedParents.Count; index++)
        {
            var parent = existingParents[index];
            var parentSeed = generatedParents[index];
            parent.FullName = parentSeed.FullName;
            parent.Email = parentSeed.Email;
            parent.Phone = parentSeed.Phone;
            parent.Address = parentSeed.Address;

            var user = await userManager.FindByIdAsync(parent.UserId) ?? await userManager.FindByEmailAsync(parent.Email);
            if (user == null)
            {
                user = await EnsureUserAsync(userManager, parent.Email, parent.FullName, ParentPassword, "Parent");
                parent.UserId = user.Id;
            }

            user.FullName = parent.FullName;
            user.Email = parent.Email;
            user.UserName = parent.Email;
            user.ParentId = parent.Id;
            await userManager.UpdateAsync(user);
        }

        for (var studentIndex = 0; studentIndex < students.Count; studentIndex++)
        {
            var parent = existingParents[studentIndex / StudentsPerParent];
            var student = students[studentIndex];
            student.ParentId = parent.Id;
            student.FullName = BuildStudentName(studentIndex, parent);

            if (!string.IsNullOrWhiteSpace(student.UserId))
            {
                var user = await userManager.FindByIdAsync(student.UserId);
                if (user != null)
                {
                    user.FullName = student.FullName;
                    await userManager.UpdateAsync(user);
                }
            }
        }

        await context.SaveChangesAsync();
    }

    private static async Task EnsureStudentTrackingCoverageAsync(SchoolDbContext context)
    {
        var now = DateTime.Now;
        var today = DateTime.Today;
        var students = await context.Students
            .Where(student => student.ClassRoomId.HasValue)
            .OrderBy(student => student.Id)
            .ToListAsync();

        if (students.Count == 0)
        {
            return;
        }

        foreach (var student in students)
        {
            student.IsActive = true;
        }

        var subjects = await context.Subjects
            .Where(subject => subject.IsActive && subject.ClassRoomId.HasValue)
            .ToListAsync();
        var existingGradeKeys = (await context.GradeRecords
                .Select(grade => new { grade.StudentId, grade.SubjectId, grade.GradeType })
                .ToListAsync())
            .Select(grade => $"{grade.StudentId}:{grade.SubjectId}:{grade.GradeType}")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var gradeTypes = new[] { "واجب", "اختبار قصير", "مشاركة" };
        var newGrades = new List<GradeRecord>();

        foreach (var student in students)
        {
            foreach (var subject in subjects.Where(subject => subject.ClassRoomId == student.ClassRoomId))
            {
                for (var index = 0; index < gradeTypes.Length; index++)
                {
                    var key = $"{student.Id}:{subject.Id}:{gradeTypes[index]}";
                    if (!existingGradeKeys.Add(key))
                    {
                        continue;
                    }

                    var scoreSeed = Math.Abs(HashCode.Combine(student.Id, subject.Id, index)) % 29;
                    newGrades.Add(new GradeRecord
                    {
                        StudentId = student.Id,
                        SubjectId = subject.Id,
                        GradeType = gradeTypes[index],
                        Score = 70 + scoreSeed,
                        Date = today.AddDays(-14 + (index * 5)),
                        Notes = "رصد تجريبي مرتبط بملف الطالب"
                    });
                }
            }
        }

        if (newGrades.Count > 0)
        {
            context.GradeRecords.AddRange(newGrades);
        }

        var endedSessions = await context.Sessions
            .Where(session => session.SessionDate < today || (session.SessionDate == today && session.EndTime <= now.TimeOfDay))
            .ToListAsync();
        var existingAttendanceKeys = (await context.Attendances
                .Select(attendance => new { attendance.StudentId, attendance.SessionId })
                .ToListAsync())
            .Select(attendance => $"{attendance.StudentId}:{attendance.SessionId}")
            .ToHashSet();
        var newAttendance = new List<Attendance>();

        foreach (var session in endedSessions)
        {
            foreach (var student in students.Where(student => student.ClassRoomId == session.ClassRoomId))
            {
                var key = $"{student.Id}:{session.Id}";
                if (!existingAttendanceKeys.Add(key))
                {
                    continue;
                }

                var roll = Math.Abs(HashCode.Combine(student.Id, session.Id)) % 100;
                var status = roll switch
                {
                    < 8 => "Absent",
                    < 15 => "Late",
                    _ => "Present"
                };
                var recordedTime = session.SessionDate.Date
                    .Add(session.StartTime)
                    .AddMinutes(status == "Late" ? 12 : 3);

                newAttendance.Add(new Attendance
                {
                    StudentId = student.Id,
                    SessionId = session.Id,
                    IsPresent = status != "Absent",
                    Status = status,
                    Notes = status == "Absent" ? "غياب بدون عذر مسجل" : null,
                    Time = recordedTime,
                    RecordedAt = recordedTime.AddMinutes(5),
                    Method = session.AttendanceType
                });
            }
        }

        if (newAttendance.Count > 0)
        {
            context.Attendances.AddRange(newAttendance);
        }

        await context.SaveChangesAsync();
    }

    private static string BuildStudentName(int index, Parent parent)
    {
        var first = FirstNames[index % FirstNames.Length];
        var parentParts = (parent.FullName ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var middle = parentParts.ElementAtOrDefault(0) ?? MiddleNames[(index / FirstNames.Length) % MiddleNames.Length];
        var family = parentParts.ElementAtOrDefault(1) ?? FamilyNames[(index / (FirstNames.Length * MiddleNames.Length)) % FamilyNames.Length];
        return $"{first} {middle} {family}";
    }

    private static DateTime GetSchoolWeekStart(DateTime date)
    {
        var daysSinceSaturday = ((int)date.DayOfWeek - (int)DayOfWeek.Saturday + 7) % 7;
        return date.Date.AddDays(-daysSinceSaturday);
    }

    private static bool HasSessionEnded(Session session, DateTime now)
    {
        var sessionEnd = session.SessionDate.Date.Add(session.EndTime);
        return sessionEnd <= now;
    }

    private sealed record TeacherSeed(string FullName, string Email, string Phone, string Specialty);

    private sealed record ParentSeed(string FullName, string Email, string Phone, string Address);

    private sealed record SubjectTemplate(
        string Name,
        string Code,
        string Term,
        string TeacherEmail,
        string Description);
}
