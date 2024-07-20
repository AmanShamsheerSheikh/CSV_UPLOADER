using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace api.Models
{
    public class BatchUpload
    {
        // public Boolean isUploaded {get;set;}
        public int BatchNumber { get; set; }
        public string command { get; set; }
        public string fileId { get; set; }
    }
}