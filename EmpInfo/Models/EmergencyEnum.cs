using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace EmpInfo.Models
{
    public enum EmergencyEnum
    {
        影响多岗位停产 = 1,
        影响单岗位停产 = 2,
        不停产 = 3,
        安全生产隐患 = 4
    }
}