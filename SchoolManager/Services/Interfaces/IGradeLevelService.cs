using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SchoolManager.Models;

public interface IGradeLevelService
{
    Task<IEnumerable<GradeLevel>> GetAllAsync();
    Task<GradeLevel?> GetByIdAsync(Guid id);
    Task<GradeLevel> CreateAsync(GradeLevel gradeLevel);
    Task<GradeLevel> UpdateAsync(GradeLevel gradeLevel);
    Task<bool> DeleteAsync(Guid id);
    Task<GradeLevel> GetOrCreateAsync(string name);
    Task<GradeLevel?> GetByNameAsync(string name);
    Task<GradeLevel?> ResolveByNameAsync(string name);


}
