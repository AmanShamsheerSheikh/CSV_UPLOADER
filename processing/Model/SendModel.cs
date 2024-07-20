using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace processing.Models
{
    public class SendModel
    {

        public required byte[] Command { get; set; }
        public required BatchUpload Batch { get; set; }
    }
}