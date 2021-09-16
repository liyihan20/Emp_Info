using System;
using System.Collections.Generic;

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
            this.isContinue = al.is_continue ? "是" : "否";
            this.hasAttachment = al.has_attachment ?? false;
            this.goWhere = al.go_where;
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
        public string isContinue { get; set; }
        public string agentMan { get; set; }
        public string informMan { get; set; }
        public bool hasAttachment { get; set; }
        public string account { get; set; }
        public string goWhere { get; set; }
    }

    public class AttachmentModel
    {
        public string fileName { get; set; }
        public long fileSize { get; set; }
        public string ext { get; set; }
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
        public string auditorName { get; set; }
        public string auditorNumber { get; set; }
        public bool? isPass { get; set; }
        public string opinion { get; set; }
        public string billType { get; set; }
        public string billTypeName { get; set; }
        public object otherInfo { get; set; }
    }

    public class ALRecordModel
    {
        public string sysNo { get; set; }
        public string applyTime { get; set; }
        public string leaveType { get; set; }
        public string leaveDays { get; set; }
        public string leaveDateSpan { get; set; }
    }

    public class ApplyMenuItemModel
    {
        public string url { get; set; }
        public string text { get; set; }
        public string iconFont { get; set; }
        public string colorClass { get; set; }
        public string linkClass { get; set; }
    }

    public class ApplyNavigatorModel
    {
        public string url { get; set; }
        public string text { get; set; }
    }

    public class ALBeforeApplyModel
    {
        public string sysNum { get; set; }
        public string depName { get; set; }
        public string depNum { get; set; }
        public int? depId { get; set; }
        public int? empLevel { get; set; }
        public int times { get; set; }
        public int days { get; set; }
        public decimal hours { get; set; }
        public string pLevels { get; set; }
        public decimal? vacationDaysLeft { get; set; }
    }

    public class EPBeforeApplyModel
    {
        public string sysNum { get; set; }
        public string procDepInfo { get; set; }
        public string applierPhone { get; set; }
    }

    public class UCBeforeApplyModel
    {
        public string sysNum { get; set; }
        public List<string> marketList { get; set; }
        public List<string> busDepList { get; set; }
        public List<string> accountingList { get; set; }
    }

    public class UCCheckApplyModel
    {
        public ei_ucApply uc { get; set; }
        public List<ei_ucApplyEntry> entrys { get; set; }
    }

    public class SABeforeApplyModel
    {
        public string sysNum { get; set; }
        public List<K3AccountModel> accounts { get; set; }
    }

    public class SAK3StockAuditor
    {
        public string stockName { get; set; }
        public string stockNum { get; set; }
        public string auditorName { get; set; }
        public string auditorNum { get; set; }
    }

    public class CRSVBeforeApplyModel
    {
        public string sysNum { get; set; }
        public string depName { get; set; }
        public string depNum { get; set; }
        public int? depId { get; set; }
    }

    public class DPCheckApplyModel
    {
        public ei_dormRepair bill { get; set; }
        public List<FlowSvr.FlowRecordModels> records { get; set; }
        public List<ei_dormRepairIems> items { get; set; }
    }

    public class PPCheckApplyModel
    {
        public ei_PPApply bill { get; set; }
        public List<ei_dormRepairIems> items { get; set; }
    }

    public class ETBeforeApplyModel
    {
        public string sysNum { get; set; }
        public string applierPhone { get; set; }
        public List<string> marketList { get; set; }
        public List<string> busDepList { get; set; }
    }

    public class ETCheckApplyModel
    {
        public ei_etApply et { get; set; }
        public List<ei_etApplyEntry> entrys { get; set; }
    }

    public class APBeforeApplyModel
    {
        public ei_apApply ap { get; set; }
        public List<string> k3AccountList { get; set; }

    }

    public class APCheckApplyModel
    {
        public ei_apApply ap { get; set; }
        public List<ei_apApplyEntry> entrys { get; set; }
    }

    public class JQBeforeApplyModel
    {
        public string sysNum { get; set; }
        public string applierNum { get; set; }
    }

    public class JQAuditOtherInfoModel
    {
        public string work_evaluation { get; set; }
        public string work_comment { get; set; }
        public string wanna_employ { get; set; }
        public string employ_comment { get; set; }
        public string quit_type { get; set; }
        public string leave_date { get; set; }
    }

    public class SPBeforeApplyModel
    {
        public string sys_no { get; set; }
        public string applier_phone { get; set; }
        public string send_or_receive { get; set; }
        public string company { get; set; }
        public string aging { get; set; }
        public string bus_name { get; set; }
        public string content_type { get; set; }
        public string from_addr { get; set; }
        public string to_addr { get; set; }
        public string receiver_name { get; set; }
        public string receiver_phone { get; set; }
        public string scope { get; set; }
        public List<string> busDepList { get; set; }
        public List<string> stockAddrList { get; set; }
    }

    public class SPExInfoModel
    {
        public string FName { get; set; }
        public string FDelivery { get; set; }
        public decimal FReed { get; set; }
    }

    public class ITAuditOtherInfoModel
    {
        public string faultyItems { get; set; }
        public string loginName { get; set; }
        public string loginPassword { get; set; }
        public int qty { get; set; }
    }

    public class DEBeforeApplyModel
    {
        public string sys_no { get; set; }
        public string applier_name { get; set; }
        public List<ei_DESubjects> subjects { get; set; }
        public List<ei_DENames> names { get; set; }
    }

    public class DEAuditOtherInfoModel
    {
        public string billJson { get; set; }
        public string entryJson { get; set; }
        public List<ei_DESubjects> subjects { get; set; }
        public List<ei_DENames> names { get; set; }
    }

    public class KSBeforeApplyModel
    {
        public string sys_no { get; set; }
        public string applier_name { get; set; }
        public string applier_number { get; set; }
        public string dep_name { get; set; }

    }

    public class KSAuditOtherInfoModel
    {        
        public string level_name { get; set; }
        public int level_reward { get; set; }
    }

    public class HHBeforeApplyModel
    {
        public string sys_no { get; set; }
        public List<string> agencyList { get; set; }
        public List<string> depNameList { get; set; }
    }

    public class HHCheckApplyModel
    {
        public ei_hhApply head { get; set; }
        public List<ei_hhApplyEntry> entrys { get; set; }
        public List<ei_hhReturnDetail> rs { get; set; }
        public List<StepNameAndAuditor> auditorList { get; set; }

    }

    public class HHSearchReportModel
    {
        public string sysNo { get; set; }
        public DateTime beginDate { get; set; }
        public DateTime endDate { get; set; }
        public string applierName { get; set; }
        public string moduel { get; set; }
        public string orderNo { get; set; }
        public string customerName { get; set; }
        public string returnDep { get; set; }
        public string auditResult { get; set; }
    }

    public class StepNameAndAuditor
    {
        public string stepName { get; set; }
        public string auditorName { get; set; }
        public string auditorNo { get; set; }
        public DateTime? auditTime { get; set; }
    }

    public class XABeforeApplyModel
    {
        public string sys_no { get; set; }
        public string applierName { get; set; }
        public string applierPhone { get; set; }
        public List<string> depNameList { get; set; }
    }

    public class XACheckApplyModel
    {
        public ei_xaApply bill;
        public List<ei_xaApplySupplier> suppliers;
    }

    public class XAFeeShareModel
    {
        public string n { get; set; }
        public int v { get; set; }
    }

    public class XASearchReportModel
    {
        public string sysNo { get; set; }
        public string classification { get; set; }
        public string applierName { get; set; }
        public DateTime fromDate { get; set; }
        public DateTime toDate { get; set; }
        public string projectName { get; set; }
        public string deptName { get; set; }
        public string currentNode { get; set; }
    }

    public class XASummaryDetailModel
    {
        public string 流水号 { get; set; }
        public string 申请部门 { get; set; }
        public string 确认日期 { get; set; }
        public string 申请人 { get; set; }
        public string 项目名称 { get; set; }
        public string 是否PO { get; set; }
        public string 中标供应商 { get; set; }
        public string 价格 { get; set; }
        public string 是否分摊单 { get; set; }
    }

    public class MTUpdateEqInfoModel
    {
        public object info { get; set; }
        public List<SelectModel> classesList { get; set; }
        public List<vw_ep_dep> depList { get; set; }
        public List<string> fileNoList { get; set;}
        public string myClassId { get; set; }
    }

    public class MTBillModel
    {
        public ei_mtApply apply { get; set; }
        public ei_mtEqInfo eqInfo { get; set; }
        public ei_mtFile file { get; set; }
        public ei_mtClass cla { get; set; }
    }

    public class XBCheckApplyModel
    {
        public ei_xbApply bill;
        public List<ei_xaApplySupplier> suppliers;
    }

    public class XBBeforeApplyModel
    {
        public string sys_no { get; set; }
        public string applierName { get; set; }
        public List<string> depNameList { get; set; }
    }

    public class XBSearchReportModel
    {
        public string sysNo { get; set; }
        public string dealType { get; set; }
        public string applierName { get; set; }
        public DateTime fromDate { get; set; }
        public DateTime toDate { get; set; }
        public string equitmentName { get; set; }
    }

    public class XCBeforeApplyModel
    {
        public string sys_no { get; set; }
        public string applierName { get; set; }
        public List<string> k3Accounts { get; set; }
        public List<ei_xcDepTarget> depList { get; set; }
        public List<ei_xcProcessDep> proDepList { get; set; }
        public string planner_auditor { get; set; }
        public string dep_charger { get; set; }
        public string buyer_auditor { get; set; }
    }

    public class XCCheckApplyModel
    {
        public ei_xcApply bill { get; set; }
        //public List<ei_xcMatOutDetail> mats { get; set; }
        //public List<ei_xcProductInDetail> pros { get; set; }
        public List<StepNameAndAuditor> auditorList { get; set; }
        //public List<POInfoModel> k3StockInfo { get; set; }
        public List<ei_xcProduct> pros { get; set; }
    }

    public class XCSearchReportModel
    {
        public string sysNo { get; set; }
        public string applierName { get; set; }
        public DateTime fromDate { get; set; }
        public DateTime toDate { get; set; }
        public string depName { get; set; }
        public string productModel { get; set; }
    }

    public class XDBeforeApplyModel
    {
        public string sys_no { get; set; }
        public List<string> k3Accounts { get; set; }
        public List<string> depList { get; set; }
    }

    public class FXSelectTypeNameModel
    {
        public List<ei_fxType> fxTypes { get; set; }
        public string lastTypeName { get; set; }
        public string lastTypeNo { get; set; }
    }

    public class FXBeforeApplyModel
    {
        public string sysNo { get; set; }
        public ei_fxType fxType { get; set; }
        public List<SelectModel> depNames { get; set; }
        public string typeNames { get; set; }
        public string applierName { get; set; }
        public string typeDemands { get; set; }
        public string applierPhone { get; set; }
        public string company { get; set; }
        public string busName { get; set; }
    }

    public class FXCheckApplyModel
    {
        public ei_fxApply bill { get; set; }
        public List<ei_fxApplyEntry> entrys { get; set; }
    }

}