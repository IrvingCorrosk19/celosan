using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SchoolManager.Models;
using SchoolManager.Dtos;
using SchoolManager.Interfaces;
using SchoolManager.Services.Interfaces;

namespace SchoolManager.Services
{
    public class StudentService : IStudentService
    {
        private readonly SchoolDbContext _context;
        private readonly ICurrentUserService _currentUserService;

        public StudentService(SchoolDbContext context, ICurrentUserService currentUserService)
        {
            _context = context;
            _currentUserService = currentUserService;
        }

        public async Task<List<Student>> GetAllAsync() =>
            await _context.Students.ToListAsync();

        public async Task<Student?> GetByIdAsync(Guid id) =>
            await _context.Students.FindAsync(id);

        public async Task CreateAsync(Student student)
        {
            _context.Students.Add(student);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateAsync(Student student)
        {
            _context.Students.Update(student);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(Guid id)
        {
            var student = await _context.Students.FindAsync(id);
            if (student != null)
            {
                _context.Students.Remove(student);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<List<Student>> GetByGroupAsync(string groupName) =>
            await _context.Students
                .Where(s => s.GroupName == groupName)
                .ToListAsync();

        public async Task<IEnumerable<StudentBasicDto>> GetByGroupAndGradeAsync(Guid groupId, Guid gradeId)
        {
            // MEJORADO: Filtrar solo estudiantes con asignaciones activas
            var result = await (from sa in _context.StudentAssignments
                                join student in _context.Users on sa.StudentId equals student.Id
                                join grade in _context.GradeLevels on sa.GradeId equals grade.Id
                                join grupo in _context.Groups on sa.GroupId equals grupo.Id
                                where (student.Role == "estudiante" || student.Role == "student" || student.Role == "alumno")
                                      && sa.GroupId == groupId
                                      && sa.GradeId == gradeId
                                      && sa.IsActive // Solo asignaciones activas
                                orderby student.LastName, student.Name
                                select new StudentBasicDto
                                {
                                    StudentId = student.Id,
                                    FullName = $"{student.LastName}, {student.Name}",  // Apellido, Nombre
                                    GradeName = grade.Name,
                                    GroupName = grupo.Name,
                                    DocumentId = student.DocumentId ?? ""
                                }).ToListAsync();

            return result;
        }

        public async Task<IEnumerable<StudentBasicDto>> GetBySubjectGroupAndGradeAsync(Guid subjectId, Guid groupId, Guid gradeId)
        {
            var subjectAssignmentIds = await _context.SubjectAssignments
                .Where(suj => suj.SubjectId == subjectId && suj.GroupId == groupId && suj.GradeLevelId == gradeId)
                .Select(suj => suj.Id)
                .ToListAsync();

            var result = await (from ssa in _context.StudentSubjectAssignments
                                join student in _context.Users on ssa.StudentId equals student.Id
                                join suj in _context.SubjectAssignments on ssa.SubjectAssignmentId equals suj.Id
                                join grade in _context.GradeLevels on suj.GradeLevelId equals grade.Id
                                join grupo in _context.Groups on suj.GroupId equals grupo.Id
                                where (student.Role == "estudiante" || student.Role == "student" || student.Role == "alumno")
                                      && ssa.IsActive
                                      && subjectAssignmentIds.Contains(ssa.SubjectAssignmentId)
                                orderby student.LastName, student.Name
                                select new StudentBasicDto
                                {
                                    StudentId = student.Id,
                                    FullName = $"{student.LastName}, {student.Name}",
                                    GradeName = grade.Name,
                                    GroupName = grupo.Name,
                                    DocumentId = student.DocumentId ?? ""
                                }).Distinct().ToListAsync();

            return result;
        }
    }
}
