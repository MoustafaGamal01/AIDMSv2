using AIDMS.Entities;
using AIDMS.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Google.Cloud.Vision.V1;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AIDMS.DTOs;
using AIDMS.Security_Entities;

namespace AIDMS.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class RegestrationController : ControllerBase
    {
        private readonly AIDMSContextClass _context;
        private readonly INotificationRepository _notificationRepo;
        private readonly IApplicationRepository _appRepo;
        private readonly IStudentRepository _studentRepository;
        private readonly IUniversityListNIdsRepository _universityListNIds;
        private readonly IDocumentRepository _documentRepository;
        private readonly IConfiguration _configuration;
        private readonly IGoogleCloudVisionRepository _visionRepository;
        private readonly IGoogleCloudStorageRepository _storageRepository;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private static string _nationalId = null;
        private static string _studentName = null;
        private static int _studentId = 0;

        // Temporary storage for documents
        private static readonly Dictionary<int, List<AIDocument>> _tempDocumentStorage = new();

        public RegestrationController(INotificationRepository notificationRepo,IApplicationRepository _appRepo, IStudentRepository studentRepository, AIDMSContextClass context, IUniversityListNIdsRepository universityListNIds, IDocumentRepository documentRepository,
            IConfiguration configuration, IGoogleCloudVisionRepository visionRepository, IGoogleCloudStorageRepository storageRepository,
            UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager)
        {
            _notificationRepo = notificationRepo;
            this._appRepo = _appRepo;
            _studentRepository = studentRepository;
            _context = context;
            _universityListNIds = universityListNIds;
            _documentRepository = documentRepository;
            _configuration = configuration;
            _visionRepository = visionRepository;
            _storageRepository = storageRepository;
            _userManager = userManager;
            _signInManager = signInManager;
        }

        [HttpPost("validate-national-id")]
        public async Task<IActionResult> ValidateNationalID([FromBody] string NationalId)
        {
            var existingNationalId = await _universityListNIds.CheckExistanceOfNationalId(NationalId);
            if (existingNationalId == null)
                return BadRequest("National ID not found");

            var existingStudent = await _studentRepository.GetStudentByNationalIdAsync(NationalId);
            
            if (existingStudent != null && existingStudent.regestrationStatus == false)
            {
                return Ok("You application pending!");
            }
            if (existingStudent != null && existingStudent.regestrationStatus == true)
            {
                return Ok("Your application is Accepted!");
            }
            _nationalId = NationalId;
            _studentName = existingNationalId.Name;

            return Ok("Your id is Valid!");
        }

        private int CalculateAge(DateTime dateOfBirth)
        {
            DateTime now = DateTime.Now;
            int age = now.Year - dateOfBirth.Year;
            if (now < dateOfBirth.AddYears(age))
                age--;
            return age;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] StudentRegisterationDto model)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            if (_nationalId == null)
                return BadRequest("You must add National Id in step 1 first");

            // Check if the email already exists
            var existingUser = await _userManager.FindByEmailAsync(model.Email);
            if (existingUser != null)
                return BadRequest("Email already in use");

            var existingUsername = await _userManager.FindByNameAsync(model.Username);
            if (existingUsername != null)
                return BadRequest("username already in use");

            // Step 3: Create Student Record without storing the plain password
            var student = new Student
            {
                firstName = model.firstName, // From Google Vision Model
                lastName = model.lastName, // From Google Vision Model
                TotalPassedHours = 0,
                Level = 1,
                PhoneNumber = model.PhoneNumber,
                militaryEducation = false,
                regestrationStatus = null,
                DepartmentId = 1,
                userName = model.Username,
                dateOfBirth = model.dateOfBirth,
                GPA = 0,
                IsMale = model.isMale,
                studentPicture = model.profilePicture,
                Age = CalculateAge(model.dateOfBirth)
            };
            student.SID = _nationalId;
            bool? ok = await _studentRepository.AddStudentAsync(student);
            _studentId = student.Id; // Set the student ID here
            if (ok == false)
                return BadRequest("Error, Please Check The Info Again!");

            // Handling userManagerProbs
            ApplicationUser applicationUser = new ApplicationUser
            {
                UserName = model.Username,
                Email = model.Email,
                PhoneNumber = model.PhoneNumber,
                NationalId = _nationalId,
                StdId = student.Id,
                UserType = "Student"
            };

            var result = await _userManager.CreateAsync(applicationUser, model.Password);
            if (!result.Succeeded)
                return BadRequest(result.Errors);

            // Create Cookie
            await _userManager.AddToRoleAsync(applicationUser, "Student");
            await _signInManager.SignInAsync(applicationUser, isPersistent: false);
            return Ok(new { Message = "Successful Registration" });
        }

        [HttpPost("upload-document")]
        public async Task<IActionResult> UploadDocument([FromForm] UploadDocumentDto uploadDocumentDto)
        {
            if (uploadDocumentDto.file == null || uploadDocumentDto.file.Length == 0)
                return BadRequest("Invalid file");

            var student = await _studentRepository.GetAllStudentDataByIdAsync(_studentId);
            if (student == null)
                return NotFound("Student not found");

            // Step 4: Upload to (TempBucket) Google Cloud Storage
            var fileUrl = await _storageRepository.UploadFileAsync(uploadDocumentDto.file);

            var validationScore = 0.0;
            switch (uploadDocumentDto.step)
            {
                case 3:
                    validationScore =
                        await _visionRepository.CheckNominationValidationAsync(uploadDocumentDto.file, _studentName);
                    break;
                case 4:
                    validationScore = await _visionRepository.CheckNationalIdValidationAsync(fileUrl, _studentName, true);
                    break;
                case 5:
                    validationScore = await _visionRepository.CheckNationalIdValidationBackAsync(fileUrl);
                    break;
                case 6:
                    validationScore = await _visionRepository.CheckBirthDateCertificateValidationAsync(fileUrl, _studentName);
                    break;
                case 7:
                    validationScore = await _visionRepository.CheckSecondaryCertificateValidationAsync(fileUrl, _studentName);
                    break;
                case 8:
                    if (await _visionRepository.CheckPhotoInImageAsync(fileUrl)) validationScore = 100.0;
                    break;
                case 9:
                    validationScore = await _visionRepository.CheckNationalIdValidationAsync(fileUrl, _studentName, false);
                    break;
                case 10:
                    validationScore = await _visionRepository.CheckNationalIdValidationBackAsync(fileUrl);
                    break;
                default:
                    return BadRequest("Invalid step");
            }

            if (validationScore < 70) // assuming 70% is the threshold
            {
                _storageRepository.DeleteFileAsync(uploadDocumentDto.file.FileName);
                return BadRequest($"Sorry, Document validation failed!");
            }
            // Step 4: Store Document Information in Dictionary
            var document = new AIDocument
            {
                
                FileName = uploadDocumentDto.file.FileName,
                FileType = uploadDocumentDto.file.ContentType,
                FilePath = fileUrl,
                UploadedAt = DateTime.Now,
                StudentId = student.Id
            };


            if (!_tempDocumentStorage.ContainsKey(_studentId))
            {
                _tempDocumentStorage[_studentId] = new List<AIDocument>();
            }
            _tempDocumentStorage[_studentId].Add(document);

            return Ok(new { Message = "Document uploaded and validated successfully", DocumentUrl = fileUrl });
        }

        [HttpPost("submit-application")]
        public async Task<IActionResult> SubmitApplication()
        {
            var student = await _studentRepository.GetAllStudentDataByIdAsync(_studentId);
            if (student == null)
                return NotFound("Student not found");
           
            // Check if all required documents are uploaded
            if (!_tempDocumentStorage.ContainsKey(_studentId) || _tempDocumentStorage[_studentId].Count < 2) // Ensure correct count check
            {
                return BadRequest("Incomplete registration process. Please complete all steps.");
            }

            var documents = _tempDocumentStorage[_studentId];

            // Create a new application
            var application = new Application
            {
                Title = "Registration Requests",
                Status = "Pending",
                isAccepted = false,
                Description = "This is a Registration Request",
                SubmittedAt = DateTime.Now,
                DecisionDate = DateTime.Now.AddMonths(1),
                ReviewDate = DateTime.Now.AddDays(7),
                StudentId = _studentId,
                Documents = documents
            };

            var std = await _studentRepository.GetStudentPersonalInfoByIdAsync(_studentId);
            await _notificationRepo.AddNotificationAsync(new Notification
            {
                Message = $"Student: {std.firstName} {std.lastName} - ID: {_studentId} \n Registered!",
                EmployeeId = null,
                //AIDocumentId = application.Documents?.FirstOrDefault()?.Id, // Nullable
                CreatedAt = DateTime.Now,
                fromStudent = true,
                StudentId = _studentId,
                IsRead = false,
            });
            
            await _appRepo.AddApplicationAsync(application);
            

            // Update document references with the application ID
            /*foreach (var document in documents)
            {
                document.ApplicationId = application.Id;
                await _documentRepository.AddDocumentAsync(document);
            }*/

            // Remove documents from temporary storage
            _tempDocumentStorage.Remove(_studentId);
            student.regestrationStatus = false; 
            return Ok("Application submitted successfully.");
        }
    }
}
