using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using processing.Models;

namespace processing.Service
{
    public class SaveLog : ISaveLog
    {
        private Log _log;

        public Log GetLog()
        {
            return _log;
        }

        public void SetLog(Log log)
        {
            _log = log;
        }
    }
}