namespace AIDMS.DTOs;

public class DocumentDto
{
    public int id { get; set; } 
    public string FilePath { get; set; }
    public string FileName { get; set; }
    public string FileType { get; set; }
    public DateTime uploadedAt { get; set; }

}