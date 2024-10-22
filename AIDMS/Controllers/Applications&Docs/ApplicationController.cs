using AIDMS.DTOs;
using AIDMS.Entities;
using AIDMS.Repositories;
using AIDMS.Security_Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;

namespace AIDMS.Controllers.Applications;

[Route("api/[controller]")]
[ApiController]
public class ApplicationController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IApplicationRepository _application;
    private readonly INotificationRepository _notification;
    private readonly IStudentRepository _student;
    public ApplicationController(UserManager<ApplicationUser> userManager, IApplicationRepository application, INotificationRepository notification
    , IStudentRepository student)
    {
        _userManager = userManager;
        _application = application;
        _notification = notification;
        _student = student;
    }

    #region Admin Applications

    [HttpGet]
    [Route("admin")]
    [Authorize(Roles = "Admin")]
    public async Task<IEnumerable<ApplicationBaseInfoDto>> GetAllApplicationsBaseInfo()
    {
        var Applications = await _application.GetAllApplicationsAsync();
        var applicationBaseInfo = Applications.Select(app => new ApplicationBaseInfoDto
        {
            Id = app.Id,
            Title = app.Title,
            Date = app.SubmittedAt,
            DecisionDate = app.DecisionDate,
            Status = app.Status
        });
        return applicationBaseInfo;
    }

    #endregion

    #region Get Application Request for the employee
    // Academic Transcript
    //Enrollment Proof
    //Material Registration
    //Military Education
    //Expenses Payment
    //Registration Requests
    [Authorize(Roles = "Affairs Officer")]
    [HttpGet]
    [Route("pending/employee")]
    [ProducesResponseType(200, Type = typeof(IEnumerable<ApplicationRequestDto>))]
    [ProducesResponseType(400)]
    public async Task<IEnumerable<ApplicationRequestDto>> GetPendingApplicationsExceptMaterial()
    {

        var Applications = await _application.GetAllPendingApplicationsAsync();

        var applicationRequestDto = Applications
            .Where(application => application.Title.ToUpper() != "Material Registration".ToUpper() &&
                                application.Title.ToUpper() != "Registration Requests".ToUpper())
            .Select(app => new ApplicationRequestDto
            {
                Id = app.Id,
                Name = app.Title,
                Date = app.SubmittedAt,
                From = $"{app.Student.firstName} {app.Student.lastName}"
            });

        return applicationRequestDto;
    }

    [Authorize(Roles = "Affairs Officer")]
    [HttpGet]
    [Route("archived/employee")]
    [ProducesResponseType(200, Type = typeof(IEnumerable<ApplicationArchivedDto>))]
    public async Task<IEnumerable<ApplicationArchivedDto>> GetArchivedApplicationsExceptMaterial()
    {
        var Applications = await _application.GetAllArchivedApplicationsAsync();
        var applicationArchivedDto = Applications
            .Where(application => application.Title.ToUpper() != "Material Registration".ToUpper() &&
                                application.Title.ToUpper() != "Registration Requests".ToUpper())
            .Select(app => new ApplicationArchivedDto
            {
                Id = app.Id,
                Name = app.Title,
                Date = app.SubmittedAt,
                From = $"{app.Student.firstName} {app.Student.lastName}",
                IsAccepted = app.isAccepted
            });
        return applicationArchivedDto;
    }


    #endregion

    #region Get Application Request for the employee by search

    [Authorize(Roles = "Affairs Officer")]
    [HttpGet]
    [Route("pending/employee/{studentName}")]
    [ProducesResponseType(200, Type = typeof(IEnumerable<ApplicationRequestDto>))]
    public async Task<IEnumerable<ApplicationRequestDto>> GetSearchInPendingApplicationsExceptMaterial(string studentName)
    {
        var Applications = await GetPendingApplicationsExceptMaterial();
        var applicationRequestDto = Applications
            .Where(app => app.From.Replace(" ", "").ToUpper()
                .Contains(studentName.Replace(" ", "").ToUpper()));
        return applicationRequestDto;
    }

    [Authorize(Roles = "Affairs Officer")]
    [HttpGet]
    [Route("archived/employee/{studentName}")]
    [ProducesResponseType(200, Type = typeof(IEnumerable<ApplicationArchivedDto>))]
    public async Task<IEnumerable<ApplicationArchivedDto>> GetSearchInArchivedApplicationsExceptMaterial(string studentName)
    {
        var Applications = await GetArchivedApplicationsExceptMaterial();
        var applicationArchivedDto = Applications
                .Where(app => app.From.Replace(" ", "").ToUpper()
                    .Contains(studentName.Replace(" ", "").ToUpper()));
        return applicationArchivedDto;
    }




    #endregion

    #region Registeration

    [Authorize(Roles = "Affairs Officer")]
    [HttpGet]
    [Route("pending/registeration")]
    [ProducesResponseType(200, Type = typeof(IEnumerable<RegisterationDto>))]
    public async Task<IEnumerable<RegisterationDto>> GetPendingRegisteration()
    {
        var Applications = await _application.GetAllPendingApplicationsAsync();
        var registerationDto = Applications
            .Where(application => application.Title.ToUpper() == "Registration Requests".ToUpper())
            .Select(app => new RegisterationDto
            {
                Id = app.Id,
                Date = app.SubmittedAt,
                Name = $"{app.Student.firstName} {app.Student.lastName}",
            });
        return registerationDto;
    }

    [Authorize(Roles = "Affairs Officer")]
    [HttpGet]
    [Route("archived/registeration")]
    [ProducesResponseType(200, Type = typeof(IEnumerable<RegisterationDto>))]
    public async Task<IEnumerable<RegisterationDto>> GetArchivedRegisteration()
    {
        var Applications = await _application.GetAllArchivedApplicationsAsync();
        var registerationArchivedDto = Applications
            .Where(application => application.Title.ToUpper() != "Registration Requests".ToUpper())
            .Select(app => new RegisterationDto
            {
                Id = app.Id,
                Date = app.SubmittedAt,
                Name = $"{app.Student.firstName} {app.Student.lastName}",
            });
        return registerationArchivedDto;
    }


    #endregion

    #region Accept & Decline Registeration

    [Authorize(Roles = "Affairs Officer")]
    [HttpDelete("decline/registeration/{appId}")]
    [ProducesResponseType(400)]
    public async Task<IActionResult> deleteRegisterationApplicationStatus(int appId)
    {
        var application = await _application.GetApplicationByIdAsync(appId);
        int studentId = (int)application.StudentId;
        var existStd = await _student.GetAllStudentDataByIdAsync(studentId);
        bool? affected = await _application.DeleteApplicationAsync(appId);
        if (affected == null)
        {
            return BadRequest();
        }

        var user = await _userManager.FindByNameAsync(existStd.userName);
        if (user == null)
        {
            return BadRequest("Student isn't found");
        }

        var result = await _userManager.DeleteAsync(user);
        if (!result.Succeeded)
        {
            return BadRequest($"Failed to delete the user associated with student id: {studentId}.");
        }
        affected = await _student.DeleteStudentAsync(studentId);
        if (affected == null)
        {
            return BadRequest();
        }
        return Ok("Student is deleted");
    }

    #endregion

    #region Accept & Decline

    [Authorize(Roles = "Affairs Officer, Academic Supervisor")]
    [HttpPut("accept/{empId}/{appId}")]
    [ProducesResponseType(400)]

    public async Task<IActionResult> UpdateAcceptAppStatus(int empId, int appId)
    {
        var application = await _application.GetApplicationByIdAsync(appId);

        application.isAccepted = true;
        application.Status = "archived";
        application.EmployeeId = empId;
        application.DecisionDate = DateTime.Now;
        bool? updated = await _application.UpdateApplicationAsync(appId, application);
        if (updated == null)
        {
            return BadRequest();
        }

        if (application.Title == "Registration Requests")
        {
            var stu = application.Student;
            var affected = await _student.UpdateStudentRegisterationAsync(stu);
            if (affected == null)
            {
                return BadRequest("Failed to accept");
            }
        }
        var added = await _notification.AddNotificationAsync(new Notification
        {
            Message = $"""
                       Dear {application.Student},
                       I am pleased to inform you that your recent {application.Title} request has been accepted. 
                       Thank you for your attention to this matter.
                       Best regards,
                       """,
            CreatedAt = DateTime.Now,
            StudentId = application.StudentId,
            AIDocumentId = application.Documents?.FirstOrDefault()?.Id,
            EmployeeId = empId,
            fromStudent = false
        });

        if (added == true)
        {
            return Ok();
        }

        return BadRequest();
    }

    [Authorize(Roles = "Affairs Officer, Academic Supervisor")]
    [HttpPut("decline/{empId}/{appId}")]
    [ProducesResponseType(400)]

    public async Task<IActionResult> UpdateDeclineAppStatus(int empId, int appId)
    {
        var application = await _application.GetApplicationByIdAsync(appId);

        application.isAccepted = false;
        application.Status = "archived";
        application.EmployeeId = empId;
        application.DecisionDate = DateTime.Now;
        bool? updated = await _application.UpdateApplicationAsync(appId, application);
        if (updated == null)
        {
            return BadRequest();
        }

        var added = await _notification.AddNotificationAsync(new Notification
        {
            Message = $"""
                       Dear {application.Student},
                       I apologize for declining your recent {application.Title} request.
                       Thank you for your attention to this matter.
                       Best regards,
                       """,
            CreatedAt = DateTime.Now,
            StudentId = application.StudentId,
            AIDocumentId = application.Documents?.FirstOrDefault()?.Id,
            EmployeeId = empId,
            fromStudent = false
        });

        if (added == true)
        {
            return Ok();
        }

        return BadRequest();
    }


    #endregion

    #region Get Application Request for the supervisor

    [Authorize(Roles = "Academic Supervisor")]
    [HttpGet]
    [Route("pending/supervisor/{empId:int}")]
    [ProducesResponseType(200, Type = typeof(IEnumerable<ApplicationRequestDto>))]
    public async Task<IEnumerable<ApplicationRequestDto>> GetPendingApplicationsWithMaterial(int empId)
    {
        var Applications = await _application.GetAllPendingApplicationsByEmployeeIdAsync(empId);
        var applicationRequestDto = Applications
            .Where(application => application.Title.ToUpper() == "Material Registration".ToUpper())
            .Select(app => new ApplicationRequestDto
            {
                Id = app.Id,
                Name = app.Title,
                Date = app.SubmittedAt,
                From = $"{app.Student.firstName} {app.Student.lastName}"
            });
        return applicationRequestDto;
    }

    [Authorize(Roles = "Academic Supervisor")]
    [HttpGet]
    [Route("archived/supervisor/{empId:int}")]
    [ProducesResponseType(200, Type = typeof(IEnumerable<ApplicationArchivedDto>))]
    public async Task<IEnumerable<ApplicationArchivedDto>> GetArchivedApplicationsWithMaterial(int empId)
    {
        var Applications = await _application.GetAllArchivedApplicationsByEmployeeIdAsync(empId);
        var applicationArchivedDto = Applications
            .Where(application => application.Title.ToUpper() == "Material Registration".ToUpper())
            .Select(app => new ApplicationArchivedDto
            {
                Id = app.Id,
                Name = app.Title,
                Date = app.SubmittedAt,
                From = $"{app.Student.firstName} {app.Student.lastName}",
                IsAccepted = app.isAccepted
            });
        return applicationArchivedDto;
    }

    #endregion

    #region Get Application Request for the supervisor by search

    [Authorize(Roles = "Academic Supervisor")]
    [HttpGet]
    [Route("pending/supervisor/{empId:int}/{studentName}")]
    [ProducesResponseType(200, Type = typeof(IEnumerable<ApplicationRequestDto>))]
    public async Task<IEnumerable<ApplicationRequestDto>> GetSearchInPendingApplicationsWithMaterial(int empId, string studentName)
    {
        var Applications = await GetPendingApplicationsWithMaterial(empId);
        var applicationRequestDto = Applications
                .Where(app => app.From.Replace(" ", "").ToUpper()
                    .Contains(studentName.Replace(" ", "").ToUpper()));
        return applicationRequestDto;
    }

    [Authorize(Roles = "Academic Supervisor")]
    [HttpGet]
    [Route("archived/supervisor/{empId:int}/{studentName}")]
    [ProducesResponseType(200, Type = typeof(IEnumerable<ApplicationArchivedDto>))]
    public async Task<IEnumerable<ApplicationArchivedDto>> GetSearchArchivedApplicationsWithMaterial(int empId, string studentName)
    {
        var Applications = await GetArchivedApplicationsWithMaterial(empId);
        var applicationArchivedDto = Applications
                .Where(app => app.From.Replace(" ", "").ToUpper()
                    .Contains(studentName.Replace(" ", "").ToUpper()));
        return applicationArchivedDto;
    }

    #endregion

}
