﻿namespace AIDMS.DTOs
{
    public class StudentApplicationDto
    {
        public int Id { get; set; }
        public string documentName { get; set; }
        public string status { get; set; }
        public string decisionDate { get; set; }
        public string uploadedAt { get; set; }
        public bool isAccepted { get; set; }
    }
}
