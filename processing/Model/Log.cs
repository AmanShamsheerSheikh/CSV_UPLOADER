using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace processing.Models
{
    public class Log
    {
        public string fileName { get; set; }
        public string fileId { get; set; }
        public string status { get; set; }
        public int NoOfBatchesCreated { get; set; }
        public int totalNumberOfBatchesCreated { get; set; }
        public List<BatchUpload> BatchData { get; set; } = new List<BatchUpload>();

        public List<String> NotUploaded { get; set; }
    }
}