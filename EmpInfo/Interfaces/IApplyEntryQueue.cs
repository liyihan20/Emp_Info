using EmpInfo.Models;
using System.Collections.Generic;

namespace EmpInfo.Interfaces
{
    interface IApplyEntryQueue
    {
        List<flow_applyEntryQueue> GetApplyEntryQueue(System.Web.Mvc.FormCollection fc,UserInfo userInfo);
    }
}
