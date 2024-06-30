using AIDMS.Entities;
using AIDMS.Repositories;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace AIDMS.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class TestController : ControllerBase
    {
        private readonly IApplicationRepository _appRepo;

        public TestController(IApplicationRepository appRepo)
        {
            _appRepo = appRepo;
        }

        [HttpPost("test")]
        public async Task<IActionResult> SubmitApplication()
        {
            var application = new Application
            {
                Title = "Student Application",
                Status = "Pending",
                isAccepted = true,
                Description = "this is a description",
                SubmittedAt = DateTime.UtcNow,
                DecisionDate = DateTime.UtcNow.AddMonths(1),
                ReviewDate = DateTime.UtcNow.AddDays(7),
                StudentId = 1,
                EmployeeId = 7
            };

            var result = await _appRepo.AddApplicationAsync(application);

            if (result == true)
            {
                return Ok(true);
            }

            return StatusCode(500, "Internal server error");
        }
    }
}