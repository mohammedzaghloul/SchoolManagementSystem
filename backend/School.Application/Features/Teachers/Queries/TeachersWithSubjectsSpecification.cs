using School.Application.Specifications;
using School.Domain.Entities;

namespace School.Application.Features.Teachers.Queries;

internal sealed class TeachersWithSubjectsSpecification : BaseSpecification<Teacher>
{
    public TeachersWithSubjectsSpecification()
    {
        AddInclude(teacher => teacher.Subjects);
    }

    public TeachersWithSubjectsSpecification(int id)
        : base(teacher => teacher.Id == id)
    {
        AddInclude(teacher => teacher.Subjects);
    }
}
