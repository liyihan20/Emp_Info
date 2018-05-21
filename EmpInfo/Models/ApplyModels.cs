using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace EmpInfo.Models
{
    public class AuditModel
    {
        public bool isAuditing { get;set; }
        public int step { get; set; }
        public string stepName { get; set; }
        public string cardNumber { get; set; }
        public string userName { get; set; }
    }

    public class ALApplyModel
    {
        public ALApplyModel() { }
        public ALApplyModel(ei_askLeave al)
        {
            this.sysNo = al.sys_no;
            this.depLongName = al.dep_long_name;
            this.applyTime = al.apply_time == null ? "" : ((DateTime)al.apply_time).ToString("yyyy-MM-dd HH:mm:ss");
            this.isDirectCharge = al.is_direct_charge == true ? "是" : "否";
            this.leaveType = al.leave_type;
            this.leaveReason = al.leave_reason;
            this.leaveTimeZone = ((DateTime)al.from_date).ToString("yyyy-MM-dd HH:mm") + " ~ " + ((DateTime)al.to_date).ToString("yyyy-MM-dd HH:mm");
            this.totalLeaveDays = "共 " + al.work_days + " 天 " + al.work_hours + " 小时";
            this.hasAttachment = al.has_attachment ?? false;
        }
        public string auditStatus { get; set; }
        public string sysNo { get; set; }
        public string applierNameAndCard { get; set; }
        public string applyTime { get; set; }
        public string depLongName { get; set; }
        public string empLevel { get; set; }
        public string isDirectCharge { get; set; }
        public string leaveType { get; set; }
        public string leaveReason { get; set; }
        public string leaveTimeZone { get; set; }
        public string totalLeaveDays { get; set; }
        public string agentMan { get; set; }
        public string informMan { get; set; }
        public bool hasAttachment { get; set; }
        public List<AttachmentModel> attachments { get; set; }
    }

    public class AttachmentModel
    {
        public string fileName { get; set; }
        public string fileSize { get; set; }
    }

    public class FlowQueueModel
    {
        public string sys_no { get; set; }
        public int step { get; set; }
        public string step_name { get; set; }
        public string auditors { get; set; }
    }

    public class BeginAuditModel
    {
        public string sysNum { get; set; }
        public int step { get; set; }
        public string stepName { get; set; }
        public bool? isPass { get; set; }
        public string opinion { get; set; }
    }
        

}