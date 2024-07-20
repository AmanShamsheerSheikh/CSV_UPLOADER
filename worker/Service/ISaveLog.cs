using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using worker.Models;

namespace processing.Service
{
    public interface ISaveLog
    {
        void SetLog(Log log);
        Log GetLog();
    }
}