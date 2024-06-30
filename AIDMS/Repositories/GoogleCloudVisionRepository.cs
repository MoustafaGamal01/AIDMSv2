using Google.Cloud.Vision.V1;
using Google.Apis.Auth.OAuth2;
using Google.Protobuf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using AIDMS.Entities;
using AIDMS.Repositories;
using Google.Protobuf.Collections;
using Grpc.Auth;

public class GoogleCloudVisionRepository : IGoogleCloudVisionRepository
{
    private readonly ImageAnnotatorClient _client;
    private readonly IPDFManagementRepository _pdfManagementRepository;

    public GoogleCloudVisionRepository(IOptions<GoogleCloudVisionOptions> options,IPDFManagementRepository pdfManagementRepository)
    {
        _client = InitializeClient(options.Value.ServiceAccountKeyPath);
        _pdfManagementRepository = pdfManagementRepository;
    }

    public async Task<double> CheckNationalIdValidationBackAsync(string imagePath)
    {
        List<Feature> featureList = new List<Feature>
        {
            new Feature { Type = Feature.Types.Type.TextDetection },
            new Feature { Type = Feature.Types.Type.LabelDetection }
        };

        string[] checks = { "متزوج","البطاقة","سارية","حتى" ,"طالب","انثى","مسلم" ,"انسه","اعزب"};

        try       // https:\storage.googleapis.com\testing - bohaa\card1.jpg
        {
            var response = await GetResponseAsync(imagePath, featureList);

            string text = response.Responses[0].FullTextAnnotation.Text;
            int calculatedPoints = CalculateTextPoints(text, checks);

            RepeatedField<EntityAnnotation> labels = response.Responses[0].LabelAnnotations;
            HashSet<string> labelsSet = new HashSet<string> { "Font", "Rectangle" };
            foreach (var label in labels)
            {
                if (labelsSet.Contains(label.Description) && label.Score >= 0.6)
                {
                    calculatedPoints++;
                }
            }

            int overAllPoints = 7 + labelsSet.Count;

            double validationScore = ((double)calculatedPoints / (overAllPoints)) * 100;

            return validationScore;
        }
        catch (Exception e)
        {
            Console.WriteLine($"An error occurred: {e.Message}");
            return 0.0;
        }
    }
    
    private ImageAnnotatorClient InitializeClient(string serviceAccountKeyPath)
    {
        var credential = GoogleCredential.FromFile(serviceAccountKeyPath)
            .CreateScoped(ImageAnnotatorClient.DefaultScopes);

        return new ImageAnnotatorClientBuilder
        {
            ChannelCredentials = credential.ToChannelCredentials()
        }.Build();
    }

    public async Task<IReadOnlyList<AnnotateImageResponse>> AnalyzeDocumentAsync(Image image)
    {
        var features = new List<Feature>
        {
            new Feature { Type = Feature.Types.Type.TextDetection },
        };

        var requests = new List<AnnotateImageRequest>
        {
            new AnnotateImageRequest
            {
                Image = image,
                Features = { features }
            }
        };

        try
        {
            var response = await _client.BatchAnnotateImagesAsync(requests);
            return response.Responses;
        }
        catch (Exception e)
        {
            Console.WriteLine($"An error occurred: {e.Message}");
            throw; // Propagate exception to caller
        }
    }

    public async Task<BatchAnnotateImagesResponse> GetResponseAsync(string imageUrl, List<Feature> featureList)
    {
        BatchAnnotateImagesResponse response = null;
        try
        {

            // Create an instance of ImageAnnotatorClient asynchronously
            var visionClient = await ImageAnnotatorClient.CreateAsync();

            // Create an image object with the image URI
            Image image = Image.FromUri(imageUrl);

            // Create a request with the image and features
            AnnotateImageRequest request = new AnnotateImageRequest
            {
                Image = image,
                Features = { featureList }
            };

            // Perform text detection on the image asynchronously
            response = await visionClient.BatchAnnotateImagesAsync(new[] { request });

            // Dispose the visionClient after use
            //await visionClient.DisposeAsync();
        }
        catch (Exception e)
        {
            throw new Exception("Error processing image: " + e.Message);
        }

        return response;
    }
    public async Task<double> CheckSecondaryCertificateValidationAsync(string imagePath,string studentName)
    {
        var featureList = new List<Feature>
        {
            new Feature { Type = Feature.Types.Type.TextDetection },
            new Feature { Type = Feature.Types.Type.FaceDetection },
            new Feature { Type = Feature.Types.Type.LabelDetection }
        };
    
    
        string[] checks =
        {
            "الإدارة", "العامة", "للامتحانات","وزارة", "التربية", "التعليم","امتحان", "شهادة", "إتمام", "الدراسة", "الثانوية", "العامة",
            "الدور", "الأول","العام", "الدراسي","الجلوس","الرقم", "القومي","اسم", "الطالب",
            "اللغة", "العربية","الأجنبية", "الأولى","الثانية","الفلسفة", "والمنطق","علم", "النفس", "والاجتماع","الجغرافيا",
            "الاقتصاد", "والإحصاء","التربية", "الوطنية","الدينية","المجموع", "الكلي","روجعت", "جميع", "البيانات", "بالمدرسة", "ووجدت", "مطابقة",
            "شئون", "الطلبة","رئيس", "لجنة", "النظام", "والمراقبة","تنبيه", "هام","هذا", "إخطار", "بنجاح", "الطالب", "من", "كان", "مصدقا",
            "عليها", "ومختومة", "بخاتم", "شعار","ای", "کشط", "او", "تعديل", "فى", "الإخطار", "يعتبر", "لاغى"
        };
    
        try
        {
            var response = await GetResponseAsync(imagePath, featureList);
    
            // checking student name in the document.
            double nameAuthorizationScore = await CheckDocumentAuthorizationAsync(response, studentName);
            if (nameAuthorizationScore < 50) return 0.0;

            // checking document content points.
            string text = response.Responses[0].FullTextAnnotation.Text;
            int calculatedPoints = CalculateTextPoints(text, checks);

            // checking document face points.
            if (response.Responses[0].FaceAnnotations.Count == 1)
            {
                calculatedPoints += 1;
            }
    
            // checking document labels points.
            RepeatedField<EntityAnnotation> labels = response.Responses[0].LabelAnnotations;
            HashSet<string> labelsSet = new HashSet<string> { "Signature", "Paper", "Font", "Handwriting" };
            foreach (var label in labels)
            {
                if (labelsSet.Contains(label.Description) && label.Score > 0.5)
                {
                    calculatedPoints++;
                }
            }
    
            int overAllPoints = checks.Length + labelsSet.Count + 1;
            // calculating the validation score.
            double validationScore = ((double)calculatedPoints / (overAllPoints)) * 100;
    
            return validationScore;
        }
        catch (Exception e)
        {
            Console.WriteLine($"An error occurred: {e.Message}");
            return 0.0;
        }
    }

    public async Task<double> CheckBirthDateCertificateValidationAsync(string imagePath,string studentName)
    {
    
        var featureList = new List<Feature>
        {
            new Feature { Type = Feature.Types.Type.TextDetection },
            new Feature { Type = Feature.Types.Type.FaceDetection },
            new Feature { Type = Feature.Types.Type.LabelDetection }
        };
        
        string[] checks =
        {
            "مصر", "العربية", "وزارة", "الداخلية", "قطاع", "مصلحة", "الاحوال",
            "المدنية", "صورة", "قيد", "الميلاد", "المولود", "محل", "تاريخ", 
            "الديانه", "النوع", "الأب", "الجنسية", "بيانات", "الأم", "توقيع",
            "تأكد", "وجود", "طابع", "الطفولة", "فئة", "جنيه", "العلامة", 
            "المائية", "نسر", "شعار", "الجمهورية", "ثيقة", "أحوال", "مدنية"
            ,"والعلامة", "المائية،", "نسر", "شعار", "الجمهورية", "وثيقة", "أحوال", "مدنية"
        };
        try
        {
            var response = await GetResponseAsync(imagePath, featureList);
            // checking student name in the document.
            double nameAuthorizationScore = await CheckDocumentAuthorizationAsync(response, studentName);
            if (nameAuthorizationScore < 50) return 0.0;
    
            string text = response.Responses[0].FullTextAnnotation.Text;
            int calculatedPoints = CalculateTextPoints(text, checks);
    
            RepeatedField<EntityAnnotation> labels = response.Responses[0].LabelAnnotations;
            HashSet<string> labelsSet = new HashSet<string> { "Paper", "Font" };
            foreach (var label in labels)
            {
                if (labelsSet.Contains(label.Description) && label.Score > 0.5)
                {
                    calculatedPoints++;
                }
            }
    
            int overAllPoints = checks.Length + labelsSet.Count;
    
            double validationScore = ((double)calculatedPoints / (overAllPoints)) * 100;
    
            return validationScore;
        }
        catch (Exception e)
        {
            Console.WriteLine($"An error occurred: {e.Message}");
            return 0.0;
        }
    }

    public async Task<double> CheckNationalIdValidationAsync(string imagePath,string studentName, bool student)
    {
        List<Feature> featureList = new List<Feature>
        {
            new Feature { Type = Feature.Types.Type.TextDetection },
            new Feature { Type = Feature.Types.Type.FaceDetection },
            new Feature { Type = Feature.Types.Type.LabelDetection}
        };
    
        string[] checks = { "جمهورية", "مصر", "العربية", "بطاقة", "تحقيق", "الشخصية" };
    
        try       // https:\storage.googleapis.com\testing - bohaa\card1.jpg
        {
            var response = await GetResponseAsync(imagePath, featureList);
            int overAllPoints = 0;
            // checking student name in the document.
            if (student)
            {
                double nameAuthorizationScore = await CheckDocumentAuthorizationAsync(response, studentName);
                if (nameAuthorizationScore < 50) return 0.0;
                overAllPoints++;
            }

            string text = response.Responses[0].FullTextAnnotation.Text;
            int calculatedPoints = CalculateTextPoints(text, checks);
    
           
            
            // Calculating Labels Score
            RepeatedField<EntityAnnotation> labels = response.Responses[0].LabelAnnotations;
            HashSet<string> labelsSet = new HashSet<string> { "Paper", "Font", "Rectangle" };
            foreach (var label in labels)
            {
                if (labelsSet.Contains(label.Description) && label.Score >= 0.6)
                {
                    calculatedPoints++;
                }
            }

            overAllPoints = checks.Length + labelsSet.Count;
    
            double validationScore = ((double)calculatedPoints / (overAllPoints)) * 100;
    
            return validationScore;
        }
        catch (Exception e)
        {
            Console.WriteLine($"An error occurred: {e.Message}");
            return 0.0;
        }
    }
    
    public async Task<double> CheckNominationValidationAsync(IFormFile file,string studentName)
    {
        string[] checks = {
            "نتيجة", "عام", "وزارة", "التعليم", "العالي", "مكتب", "تنسيق", "القبول",
                "بالجامعات", "والمعاهد", "إخطار", "مبدني", "بالترشيح", "برنامج", "تقليل",
                "الاغتراب", "بإخطاركم", "بأنه", "قد", "ترشيحكم", "ترتيب",
                "الرغبة", "رقم", "الجلوس", "الإيصال", "مجموع", "الدرجات", "الشهادة", "تاريخ",
                "اعلان", "ملحوظات", "هامة", "الثانوية", "العامة", "الكلية", "المعهد",
                "أعلاه", "بناء", "المقدمة", "ويتم", "إلغاء", "واتخاذ", "الإجراءات",
                "اذا", "وجد", "خطأ", "أو", "تعديل", "ثبوت", "يخالف", "ورد",
                "بالاوراق", "المسلمة", "استنفاذ", "سيتم", "فتح", "التقديم", "المراحل", "التالية" };
    
        try      
        {
            string text = await _pdfManagementRepository.ReadPDFContent(file);
            text = new string(text.Reverse().ToArray());
            Console.WriteLine(text);
            // checking student name in the document.
            double nameAuthorizationScore = await CheckNominationAuthorizationAsync(text, studentName);
            if (nameAuthorizationScore < 50) return 0.0;
            
            int calculatedPoints = CalculateTextPoints(text, checks);
    
            int overAllPoints = checks.Length;
    
            double validationScore = ((double)calculatedPoints / (overAllPoints)) * 100;
    
            return validationScore;
        }
        catch (Exception e)
        {
            Console.WriteLine($"An error occurred: {e.Message}");
            return 0.0;
        }
    }
    public async Task<double> CheckNominationAuthorizationAsync(string text, string studentName)
    {
        return 100.0;
        
        try
        {
            int calculatedPoints = 0;
            string[] checks = studentName.Split(' ');


            foreach (string s in text.Split('\n'))
            {
                foreach (string ss in s.Split(' '))
                {
                    foreach (string check in checks)
                    {
                        int abs = Math.Abs(ss.Length - check.Length);
                        if ((check.Contains(ss) || ss.Contains(check)) && abs < 3)
                        {
                            calculatedPoints++;
                            break;
                        }
                    }
                }
            }
            double validationScore = ((double)calculatedPoints / checks.Length) * 100;

            return validationScore;
        }
        catch (Exception e)
        {
            Console.WriteLine($"An error occurred: {e.Message}");
            return 0.0;
        }
    }

    private int CalculateTextPoints(string text, string[] checks)
    {
        int points = 0;
        // splitting the text into words and removing empty entries.
        HashSet<string> textSet = new HashSet<string>(text.Split(new[] { ' ', '\n' }, StringSplitOptions.RemoveEmptyEntries));

        // checking the text against the checks.
        foreach (var word in textSet)
        {
            foreach (var check in checks)
            {
                int abs = Math.Abs(word.Length - check.Length);
                if ((check.Contains(word) || word.Contains(check)) && abs < 3)
                {
                    points++;
                    break;
                }

            }
        }

        return points;
    }



    public async Task<double> CheckDocumentAuthorizationAsync(BatchAnnotateImagesResponse response, string studentName)
    {
        return 100.0;
        try
        {
            string text = response.Responses[0].FullTextAnnotation.Text;
            int calculatedPoints = 0;
            string[] checks = studentName.Split(' ');


            foreach (string s in text.Split('\n'))
            {
                foreach (string ss in s.Split(' '))
                {
                    foreach (string check in checks)
                    {
                        int abs = Math.Abs(ss.Length - check.Length);
                        if ((check.Contains(ss) || ss.Contains(check)) && abs < 3)
                        {
                            calculatedPoints++;
                            break;
                        }
                    }
                }
            }
            double validationScore = ((double)calculatedPoints / checks.Length) * 100;

            return validationScore;
        }
        catch (Exception e)
        {
            Console.WriteLine($"An error occurred: {e.Message}");
            return 0.0;
        }
    }
    


    public async Task<bool> CheckPhotoInImageAsync(string imagePath)
    {
        List<Feature> featureList = new List<Feature>
        {
            new Feature { Type = Feature.Types.Type.FaceDetection }
        };

        try    
        {
            var response = await GetResponseAsync(imagePath, featureList);

            return response.Responses[0].FaceAnnotations.Count == 1;
        }
        catch (Exception e)
        {
            Console.WriteLine($"An error occurred: {e.Message}");
            return false;
        }
    }


}