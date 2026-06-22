using SchoolManager.Services.Interfaces;

namespace SchoolManager.Services.Implementations
{
    public class MenuService : IMenuService
    {
        public async Task<List<MenuItem>> GetMenuItemsForUserAsync(string role)
        {
            var allMenuItems = new List<MenuItem>
            {
                new MenuItem 
                { 
                    Title = "Dashboard", 
                    Icon = "fas fa-tachometer-alt",
                    Url = "/Home/Index",
                    RequiredRoles = new[] { "admin", "teacher", "student", "director", "superadmin", "estudiante", "secretaria" }
                },
                new MenuItem 
                { 
                    Title = "Cambiar Contraseña", 
                    Icon = "fas fa-key",
                    Url = "/ChangePassword/Index",
                    RequiredRoles = new[] { "admin", "teacher", "student", "director", "superadmin", "estudiante", "secretaria" }
                },
                new MenuItem 
                { 
                    Title = "Estudiantes", 
                    Icon = "fas fa-user-graduate",
                    Url = "/Student/Index",
                    RequiredRoles = new[] { "student", "estudiante" }
                },
                new MenuItem
                {
                    Title = "Prematrícula",
                    Icon = "fas fa-user-plus",
                    Url = "#",
                    RequiredRoles = new[] { "acudiente", "parent", "student", "estudiante" },
                    SubItems = new List<MenuItem>
                    {
                        new MenuItem
                        {
                            Title = "Mis Prematrículas",
                            Icon = "far fa-circle",
                            Url = "/Prematriculation/MyPrematriculations",
                            RequiredRoles = new[] { "acudiente", "parent", "student", "estudiante" }
                        },
                        new MenuItem
                        {
                            Title = "Nueva Prematrícula",
                            Icon = "far fa-circle",
                            Url = "/Prematriculation/Create",
                            RequiredRoles = new[] { "acudiente", "parent", "student", "estudiante" }
                        }
                    }
                },
                new MenuItem 
                { 
                    Title = "Portal Docente", 
                    Icon = "fas fa-chalkboard-teacher",
                    Url = "/TeacherGradebook/Index",
                    RequiredRoles = new[] { "teacher" }
                },
                new MenuItem 
                { 
                    Title = "Plan de Trabajo Trimestral", 
                    Icon = "fas fa-clipboard-list",
                    Url = "/TeacherWorkPlan/Index",
                    RequiredRoles = new[] { "teacher", "admin" }
                },
                new MenuItem
                {
                    Title = "Horarios",
                    Icon = "fas fa-calendar-alt",
                    Url = "#",
                    RequiredRoles = new[] { "admin", "director", "teacher" },
                    SubItems = new List<MenuItem>
                    {
                        new MenuItem
                        {
                            Title = "Cargar horarios por docente",
                            Icon = "fas fa-table",
                            Url = "/Schedule/ByTeacher",
                            RequiredRoles = new[] { "admin", "director", "teacher" }
                        },
                        new MenuItem
                        {
                            Title = "Configuración de jornada",
                            Icon = "fas fa-cog",
                            Url = "/ScheduleConfiguration/Index",
                            RequiredRoles = new[] { "admin", "director" }
                        },
                        new MenuItem
                        {
                            Title = "Ajustar bloques horarios",
                            Icon = "fas fa-clock",
                            Url = "/TimeSlot/Manage",
                            RequiredRoles = new[] { "admin", "director" }
                        }
                    }
                },
                new MenuItem 
                { 
                    Title = "Portal Director", 
                    Icon = "fas fa-user-tie",
                    Url = "/Director/Index",
                    RequiredRoles = new[] { "director" }
                },
                new MenuItem 
                { 
                    Title = "Catálogo de Asignaciones", 
                    Icon = "fas fa-clipboard",
                    Url = "/SubjectAssignment/Index",
                    RequiredRoles = new[] { "admin" }
                },
                new MenuItem 
                { 
                    Title = "Administración", 
                    Icon = "fas fa-cogs",
                    Url = "#",
                    RequiredRoles = new[] { "admin", "secretaria" },
                    SubItems = new List<MenuItem>
                    {
                        new MenuItem 
                        { 
                            Title = "Administrar Usuarios", 
                            Icon = "fas fa-users",
                            Url = "/User/Index",
                            RequiredRoles = new[] { "admin" }
                        },
                        new MenuItem 
                        { 
                            Title = "Catálogo Académico", 
                            Icon = "fas fa-layer-group",
                            Url = "/AcademicCatalog/Index",
                            RequiredRoles = new[] { "admin" }
                        },
                        new MenuItem
                        {
                            Title = "Malla y Prerrequisitos",
                            Icon = "fas fa-project-diagram",
                            Url = "/SuperAdmin/CurriculumTracks",
                            RequiredRoles = new[] { "admin", "secretaria" }
                        },
                        new MenuItem 
                        { 
                            Title = "Asignar Docentes", 
                            Icon = "fas fa-tasks",
                            Url = "/TeacherAssignment/Index",
                            RequiredRoles = new[] { "admin" }
                        },
                        new MenuItem 
                        { 
                            Title = "Asignar Estudiantes", 
                            Icon = "fas fa-tasks",
                            Url = "/StudentAssignment/Index",
                            RequiredRoles = new[] { "admin" }
                        },
                        new MenuItem 
                        { 
                            Title = "Carga Asignaciones Docentes", 
                            Icon = "fas fa-file-upload",
                            Url = "/AcademicAssignment/Upload",
                            RequiredRoles = new[] { "admin" }
                        },
                        new MenuItem 
                        { 
                            Title = "Carga Asignaciones Estudiantes", 
                            Icon = "fas fa-file-upload",
                            Url = "/StudentAssignment/Upload",
                            RequiredRoles = new[] { "admin" }
                        },
                        new MenuItem 
                        { 
                            Title = "Carnet Estudiantil", 
                            Icon = "fas fa-id-card",
                            Url = "/StudentIdCard/ui",
                            RequiredRoles = new[] { "admin", "secretaria" }
                        },
                        new MenuItem 
                        { 
                            Title = "Planes de Trabajo (Docentes)", 
                            Icon = "fas fa-clipboard-list",
                            Url = "/TeacherWorkPlan/Index",
                            RequiredRoles = new[] { "admin" }
                        },
                        new MenuItem
                        {
                            Title = "Documentos CELOSAM",
                            Icon = "fas fa-id-card",
                            Url = "/Celosan/Documents",
                            RequiredRoles = new[] { "admin", "secretaria" }
                        },
                        new MenuItem
                        {
                            Title = "Créditos / Convalidaciones",
                            Icon = "fas fa-file-import",
                            Url = "/Celosan/BulkCredits",
                            RequiredRoles = new[] { "admin", "secretaria" }
                        },
                        new MenuItem
                        {
                            Title = "Revisión de Convalidaciones",
                            Icon = "fas fa-exchange-alt",
                            Url = "/SuperAdmin/Equivalencies",
                            RequiredRoles = new[] { "admin", "secretaria" }
                        },
                        new MenuItem
                        {
                            Title = "Reportes CELOSAM",
                            Icon = "fas fa-chart-bar",
                            Url = "/Celosan/Reports",
                            RequiredRoles = new[] { "admin", "secretaria" }
                        }
                    }
                },
                new MenuItem 
                { 
                    Title = "Carnet Estudiantil", 
                    Icon = "fas fa-id-card",
                    Url = "/StudentIdCard/ui",
                    RequiredRoles = new[] { "superadmin", "secretaria" }
                },
                new MenuItem 
                { 
                    Title = "Club de Padres", 
                    Icon = "fas fa-hand-holding-usd",
                    Url = "/ClubParents/Students",
                    RequiredRoles = new[] { "clubparentsadmin" }
                }
            };

            return allMenuItems
                .Where(m => m.RequiredRoles.Contains(role.ToLower()))
                .ToList();
        }
    }
} 