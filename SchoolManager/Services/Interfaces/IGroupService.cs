using SchoolManager.Models;
public interface IGroupService
{
    Task<List<Group>> GetAllAsync();
    Task<Group?> GetByIdAsync(Guid id);
    Task<Group> CreateAsync(Group group);
    Task UpdateAsync(Group group);
    Task DeleteAsync(Guid id);
    Task<Group> GetOrCreateAsync(string name);
    Task<Group?> GetByNameAndGradeAsync(string groupName, Guid? schoolId = null, Guid? shiftId = null);

}
