using SchoolManager.Models;

public interface ISubjectService
{
    Task<List<Subject>> GetAllAsync();
    Task<Subject?> GetByIdAsync(Guid id);
    Task<Subject> CreateAsync(Subject subject);
    Task<Subject> UpdateAsync(Subject subject); // <-- Modificado para devolver Subject
    Task DeleteAsync(Guid id);
    Task<Subject> GetOrCreateAsync(string name);
    Task<Subject?> GetByCodeAsync(string code);
    Task<List<SubjectAssignment>> GetSubjectAssignmentsByGradeAndGroupAsync(Guid gradeId, Guid groupId);
    Task<List<SubjectAssignment>> GetSubjectAssignmentsByStudentAsync(Guid studentId);

}
