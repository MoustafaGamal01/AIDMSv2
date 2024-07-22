using Google.Cloud.Vision.V1;

namespace AIDMS.Repositories
{
    public interface IGoogleCloudVisionRepository
    {
        Task<IReadOnlyList<AnnotateImageResponse>> AnalyzeDocumentAsync(Image image);
        Task<BatchAnnotateImagesResponse> GetResponseAsync(string imagePath, List<Feature> featureList);

        Task<double> CheckSecondaryCertificateValidationAsync(string imagePath, string studentName);

        Task<double> CheckBirthDateCertificateValidationAsync(string imagePath, string studentName);

        Task<double> CheckNationalIdValidationAsync(string imagePath, string studentName,bool student);

        Task<bool> CheckPhotoInImageAsync(string imagePath);

        Task<double> CheckDocumentAuthorizationAsync(BatchAnnotateImagesResponse response, string studentName);
        Task<double> CheckNominationValidationAsync(IFormFile file, string studentName);
        Task<double> CheckNationalIdValidationBackAsync(string imagePath);
        Task<double> CheckNominationAuthorizationAsync(string text, string studentName);
    }
}