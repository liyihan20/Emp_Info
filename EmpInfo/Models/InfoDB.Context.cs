﻿//------------------------------------------------------------------------------
// <auto-generated>
//    此代码是根据模板生成的。
//
//    手动更改此文件可能会导致应用程序中发生异常行为。
//    如果重新生成代码，则将覆盖对此文件的手动更改。
// </auto-generated>
//------------------------------------------------------------------------------

namespace EmpInfo.Models
{
    using System;
    using System.Data.Entity;
    using System.Data.Entity.Infrastructure;
    using System.Data.Objects;
    using System.Data.Objects.DataClasses;
    using System.Linq;
    
    public partial class ICAuditEntities : DbContext
    {
        public ICAuditEntities()
            : base("name=ICAuditEntities")
        {
        }
    
        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            throw new UnintentionalCodeFirstException();
        }
    
        public DbSet<ei_users> ei_users { get; set; }
        public DbSet<ei_event_log> ei_event_log { get; set; }
        public DbSet<vw_ei_users> vw_ei_users { get; set; }
        public DbSet<ei_authority> ei_authority { get; set; }
        public DbSet<ei_groupAuthority> ei_groupAuthority { get; set; }
        public DbSet<ei_groups> ei_groups { get; set; }
        public DbSet<ei_groupUser> ei_groupUser { get; set; }
        public DbSet<dn_dishes> dn_dishes { get; set; }
        public DbSet<dn_order> dn_order { get; set; }
        public DbSet<dn_orderEntry> dn_orderEntry { get; set; }
        public DbSet<dn_desks> dn_desks { get; set; }
        public DbSet<dn_shoppingCar> dn_shoppingCar { get; set; }
        public DbSet<all_maxNum> all_maxNum { get; set; }
        public DbSet<dn_discountInfo> dn_discountInfo { get; set; }
        public DbSet<dn_discountInfoUsers> dn_discountInfoUsers { get; set; }
        public DbSet<dn_points> dn_points { get; set; }
        public DbSet<dn_pointsForDish> dn_pointsForDish { get; set; }
        public DbSet<dn_pointsRecord> dn_pointsRecord { get; set; }
        public DbSet<dn_birthdayMealLog> dn_birthdayMealLog { get; set; }
        public DbSet<dn_items> dn_items { get; set; }
        public DbSet<dn_Restaurent> dn_Restaurent { get; set; }
        public DbSet<ei_resVisitLog> ei_resVisitLog { get; set; }
        public DbSet<vw_ei_simple_users> vw_ei_simple_users { get; set; }
        public DbSet<ei_deliveryInfo> ei_deliveryInfo { get; set; }
        public DbSet<ei_pushMsg> ei_pushMsg { get; set; }
        public DbSet<ei_emp_portrait> ei_emp_portrait { get; set; }
        public DbSet<ei_PushResponse> ei_PushResponse { get; set; }
        public DbSet<ei_k3RestLog> ei_k3RestLog { get; set; }
        public DbSet<ei_dormRepair> ei_dormRepair { get; set; }
        public DbSet<vw_getAllhrEmp> vw_getAllhrEmp { get; set; }
        public DbSet<ei_department> ei_department { get; set; }
        public DbSet<ei_departmentAuditUser> ei_departmentAuditUser { get; set; }
        public DbSet<ei_empLevel> ei_empLevel { get; set; }
        public DbSet<vw_push_users> vw_push_users { get; set; }
        public DbSet<ei_users_android> ei_users_android { get; set; }
        public DbSet<ei_departmentAuditNode> ei_departmentAuditNode { get; set; }
        public DbSet<wx_pushMsg> wx_pushMsg { get; set; }
        public DbSet<ei_leaveDayExceedPushLog> ei_leaveDayExceedPushLog { get; set; }
        public DbSet<vw_leaving_days> vw_leaving_days { get; set; }
        public DbSet<ei_stockAdminApply> ei_stockAdminApply { get; set; }
        public DbSet<flow_auditorRelation> flow_auditorRelation { get; set; }
        public DbSet<ei_ucApply> ei_ucApply { get; set; }
        public DbSet<ei_ucApplyEntry> ei_ucApplyEntry { get; set; }
        public DbSet<vw_UCReport> vw_UCReport { get; set; }
        public DbSet<vw_askLeaveReport> vw_askLeaveReport { get; set; }
        public DbSet<ei_ALModifyLog> ei_ALModifyLog { get; set; }
        public DbSet<vw_assistantEmps> vw_assistantEmps { get; set; }
        public DbSet<ei_CRApply> ei_CRApply { get; set; }
        public DbSet<ei_SVApply> ei_SVApply { get; set; }
        public DbSet<vw_crReport> vw_crReport { get; set; }
        public DbSet<vw_svReport> vw_svReport { get; set; }
        public DbSet<flow_applyEntryQueue> flow_applyEntryQueue { get; set; }
        public DbSet<ei_public_fund> ei_public_fund { get; set; }
        public DbSet<public_fund_item> public_fund_item { get; set; }
        public DbSet<ei_epPrDeps> ei_epPrDeps { get; set; }
        public DbSet<ei_epEqDeps> ei_epEqDeps { get; set; }
        public DbSet<ei_epEqUsers> ei_epEqUsers { get; set; }
        public DbSet<ei_askLeave> ei_askLeave { get; set; }
        public DbSet<ei_epApply> ei_epApply { get; set; }
        public DbSet<vw_epReport> vw_epReport { get; set; }
        public DbSet<ei_epBusReportChecker> ei_epBusReportChecker { get; set; }
        public DbSet<vw_UCExcel> vw_UCExcel { get; set; }
        public DbSet<ei_etApplyEntry> ei_etApplyEntry { get; set; }
        public DbSet<ei_etApply> ei_etApply { get; set; }
        public DbSet<vw_ETExcel> vw_ETExcel { get; set; }
        public DbSet<vw_ETReport> vw_ETReport { get; set; }
        public DbSet<flow_notifyUsers> flow_notifyUsers { get; set; }
        public DbSet<ei_vacationDays> ei_vacationDays { get; set; }
        public DbSet<ei_apQtyChangeLog> ei_apQtyChangeLog { get; set; }
        public DbSet<vw_hr_department> vw_hr_department { get; set; }
        public DbSet<k3_database> k3_database { get; set; }
        public DbSet<ei_apApply> ei_apApply { get; set; }
        public DbSet<ei_apApplyEntry> ei_apApplyEntry { get; set; }
        public DbSet<vw_APExcel> vw_APExcel { get; set; }
        public DbSet<ei_jqApply> ei_jqApply { get; set; }
        public DbSet<vw_JQExcel> vw_JQExcel { get; set; }
        public DbSet<ei_spApply> ei_spApply { get; set; }
        public DbSet<ei_flowAuthority> ei_flowAuthority { get; set; }
        public DbSet<ei_spApplyEntry> ei_spApplyEntry { get; set; }
        public DbSet<vw_SPReport> vw_SPReport { get; set; }
        public DbSet<ei_ieApply> ei_ieApply { get; set; }
        public DbSet<ei_ieAuditors> ei_ieAuditors { get; set; }
        public DbSet<ei_TIApply> ei_TIApply { get; set; }
        public DbSet<ei_TIApplyEntry> ei_TIApplyEntry { get; set; }
        public DbSet<vw_TIExcel> vw_TIExcel { get; set; }
        public DbSet<ei_sjApply> ei_sjApply { get; set; }
        public DbSet<ei_sjApplyEntry> ei_sjApplyEntry { get; set; }
        public DbSet<vw_SJExcel> vw_SJExcel { get; set; }
        public DbSet<vw_spExcel> vw_spExcel { get; set; }
        public DbSet<wx_accessToken> wx_accessToken { get; set; }
        public DbSet<wx_jsApiTicket> wx_jsApiTicket { get; set; }
        public DbSet<ei_itApply> ei_itApply { get; set; }
        public DbSet<ei_itItems> ei_itItems { get; set; }
    
        public virtual ObjectResult<string> GetDormChargeMonth()
        {
            return ((IObjectContextAdapter)this).ObjectContext.ExecuteFunction<string>("GetDormChargeMonth");
        }
    
        public virtual ObjectResult<GetEmpDormInfo_Result> GetEmpDormInfo(string card_no)
        {
            var card_noParameter = card_no != null ?
                new ObjectParameter("card_no", card_no) :
                new ObjectParameter("card_no", typeof(string));
    
            return ((IObjectContextAdapter)this).ObjectContext.ExecuteFunction<GetEmpDormInfo_Result>("GetEmpDormInfo", card_noParameter);
        }
    
        public virtual ObjectResult<GetDormFeeByMonth_Result> GetDormFeeByMonth(string yearMonth, string salaryNo)
        {
            var yearMonthParameter = yearMonth != null ?
                new ObjectParameter("yearMonth", yearMonth) :
                new ObjectParameter("yearMonth", typeof(string));
    
            var salaryNoParameter = salaryNo != null ?
                new ObjectParameter("salaryNo", salaryNo) :
                new ObjectParameter("salaryNo", typeof(string));
    
            return ((IObjectContextAdapter)this).ObjectContext.ExecuteFunction<GetDormFeeByMonth_Result>("GetDormFeeByMonth", yearMonthParameter, salaryNoParameter);
        }
    
        public virtual ObjectResult<ValidateDormStatus_Result> ValidateDormStatus(string account)
        {
            var accountParameter = account != null ?
                new ObjectParameter("account", account) :
                new ObjectParameter("account", typeof(string));
    
            return ((IObjectContextAdapter)this).ObjectContext.ExecuteFunction<ValidateDormStatus_Result>("ValidateDormStatus", accountParameter);
        }
    
        public virtual ObjectResult<GetPOAccount_Result> GetPOAccount(string po_number)
        {
            var po_numberParameter = po_number != null ?
                new ObjectParameter("po_number", po_number) :
                new ObjectParameter("po_number", typeof(string));
    
            return ((IObjectContextAdapter)this).ObjectContext.ExecuteFunction<GetPOAccount_Result>("GetPOAccount", po_numberParameter);
        }
    
        public virtual int InsertIntoYFEmp(string email, string mobilephone, string shortphone, string username, string cardno)
        {
            var emailParameter = email != null ?
                new ObjectParameter("email", email) :
                new ObjectParameter("email", typeof(string));
    
            var mobilephoneParameter = mobilephone != null ?
                new ObjectParameter("mobilephone", mobilephone) :
                new ObjectParameter("mobilephone", typeof(string));
    
            var shortphoneParameter = shortphone != null ?
                new ObjectParameter("shortphone", shortphone) :
                new ObjectParameter("shortphone", typeof(string));
    
            var usernameParameter = username != null ?
                new ObjectParameter("username", username) :
                new ObjectParameter("username", typeof(string));
    
            var cardnoParameter = cardno != null ?
                new ObjectParameter("cardno", cardno) :
                new ObjectParameter("cardno", typeof(string));
    
            return ((IObjectContextAdapter)this).ObjectContext.ExecuteFunction("InsertIntoYFEmp", emailParameter, mobilephoneParameter, shortphoneParameter, usernameParameter, cardnoParameter);
        }
    
        public virtual ObjectResult<string> BindK3Emp(string k3_username, string card_number, string k3_account)
        {
            var k3_usernameParameter = k3_username != null ?
                new ObjectParameter("k3_username", k3_username) :
                new ObjectParameter("k3_username", typeof(string));
    
            var card_numberParameter = card_number != null ?
                new ObjectParameter("card_number", card_number) :
                new ObjectParameter("card_number", typeof(string));
    
            var k3_accountParameter = k3_account != null ?
                new ObjectParameter("k3_account", k3_account) :
                new ObjectParameter("k3_account", typeof(string));
    
            return ((IObjectContextAdapter)this).ObjectContext.ExecuteFunction<string>("BindK3Emp", k3_usernameParameter, card_numberParameter, k3_accountParameter);
        }
    
        public virtual ObjectResult<GetK3AccoutList_Result> GetK3AccoutList()
        {
            return ((IObjectContextAdapter)this).ObjectContext.ExecuteFunction<GetK3AccoutList_Result>("GetK3AccoutList");
        }
    
        public virtual ObjectResult<string> ResetK3Emp(string k3_username, string card_number, string phone, string k3_account, string op_type)
        {
            var k3_usernameParameter = k3_username != null ?
                new ObjectParameter("k3_username", k3_username) :
                new ObjectParameter("k3_username", typeof(string));
    
            var card_numberParameter = card_number != null ?
                new ObjectParameter("card_number", card_number) :
                new ObjectParameter("card_number", typeof(string));
    
            var phoneParameter = phone != null ?
                new ObjectParameter("phone", phone) :
                new ObjectParameter("phone", typeof(string));
    
            var k3_accountParameter = k3_account != null ?
                new ObjectParameter("k3_account", k3_account) :
                new ObjectParameter("k3_account", typeof(string));
    
            var op_typeParameter = op_type != null ?
                new ObjectParameter("op_type", op_type) :
                new ObjectParameter("op_type", typeof(string));
    
            return ((IObjectContextAdapter)this).ObjectContext.ExecuteFunction<string>("ResetK3Emp", k3_usernameParameter, card_numberParameter, phoneParameter, k3_accountParameter, op_typeParameter);
        }
    
        public virtual ObjectResult<GetDormRoomMate_Result> GetDormRoomMate(string dormNumber)
        {
            var dormNumberParameter = dormNumber != null ?
                new ObjectParameter("dormNumber", dormNumber) :
                new ObjectParameter("dormNumber", typeof(string));
    
            return ((IObjectContextAdapter)this).ObjectContext.ExecuteFunction<GetDormRoomMate_Result>("GetDormRoomMate", dormNumberParameter);
        }
    
        public virtual ObjectResult<string> GetSalaryBankCard(string salary_no)
        {
            var salary_noParameter = salary_no != null ?
                new ObjectParameter("salary_no", salary_no) :
                new ObjectParameter("salary_no", typeof(string));
    
            return ((IObjectContextAdapter)this).ObjectContext.ExecuteFunction<string>("GetSalaryBankCard", salary_noParameter);
        }
    
        public virtual ObjectResult<GetK3StockAccountList_Result> GetK3StockAccoutList()
        {
            return ((IObjectContextAdapter)this).ObjectContext.ExecuteFunction<GetK3StockAccountList_Result>("GetK3StockAccoutList");
        }
    
        public virtual ObjectResult<GetK3StockAuditor_Result> GetK3StockAuditor(string accountName)
        {
            var accountNameParameter = accountName != null ?
                new ObjectParameter("accountName", accountName) :
                new ObjectParameter("accountName", typeof(string));
    
            return ((IObjectContextAdapter)this).ObjectContext.ExecuteFunction<GetK3StockAuditor_Result>("GetK3StockAuditor", accountNameParameter);
        }
    
        public virtual ObjectResult<GenTrulyDeliveryBill_Result> GenTrulyDeliveryBill(string account, string orderNumber, string sRNumber)
        {
            var accountParameter = account != null ?
                new ObjectParameter("account", account) :
                new ObjectParameter("account", typeof(string));
    
            var orderNumberParameter = orderNumber != null ?
                new ObjectParameter("orderNumber", orderNumber) :
                new ObjectParameter("orderNumber", typeof(string));
    
            var sRNumberParameter = sRNumber != null ?
                new ObjectParameter("SRNumber", sRNumber) :
                new ObjectParameter("SRNumber", typeof(string));
    
            return ((IObjectContextAdapter)this).ObjectContext.ExecuteFunction<GenTrulyDeliveryBill_Result>("GenTrulyDeliveryBill", accountParameter, orderNumberParameter, sRNumberParameter);
        }
    
        public virtual ObjectResult<GetOutStockBillsForAudit1_Result> GetOutStockBillsForAudit1(string account, Nullable<System.DateTime> fd, Nullable<System.DateTime> td)
        {
            var accountParameter = account != null ?
                new ObjectParameter("account", account) :
                new ObjectParameter("account", typeof(string));
    
            var fdParameter = fd.HasValue ?
                new ObjectParameter("fd", fd) :
                new ObjectParameter("fd", typeof(System.DateTime));
    
            var tdParameter = td.HasValue ?
                new ObjectParameter("td", td) :
                new ObjectParameter("td", typeof(System.DateTime));
    
            return ((IObjectContextAdapter)this).ObjectContext.ExecuteFunction<GetOutStockBillsForAudit1_Result>("GetOutStockBillsForAudit1", accountParameter, fdParameter, tdParameter);
        }
    
        public virtual ObjectResult<StockbillAudit1_Result> StockbillAudit1(string account, Nullable<int> interid, string name, Nullable<bool> reject)
        {
            var accountParameter = account != null ?
                new ObjectParameter("account", account) :
                new ObjectParameter("account", typeof(string));
    
            var interidParameter = interid.HasValue ?
                new ObjectParameter("interid", interid) :
                new ObjectParameter("interid", typeof(int));
    
            var nameParameter = name != null ?
                new ObjectParameter("name", name) :
                new ObjectParameter("name", typeof(string));
    
            var rejectParameter = reject.HasValue ?
                new ObjectParameter("reject", reject) :
                new ObjectParameter("reject", typeof(bool));
    
            return ((IObjectContextAdapter)this).ObjectContext.ExecuteFunction<StockbillAudit1_Result>("StockbillAudit1", accountParameter, interidParameter, nameParameter, rejectParameter);
        }
    
        public virtual int AddUser(string card_number, string accountset)
        {
            var card_numberParameter = card_number != null ?
                new ObjectParameter("card_number", card_number) :
                new ObjectParameter("card_number", typeof(string));
    
            var accountsetParameter = accountset != null ?
                new ObjectParameter("accountset", accountset) :
                new ObjectParameter("accountset", typeof(string));
    
            return ((IObjectContextAdapter)this).ObjectContext.ExecuteFunction("AddUser", card_numberParameter, accountsetParameter);
        }
    
        public virtual ObjectResult<Nullable<decimal>> GetVacationDaysLeftProc(string empNo)
        {
            var empNoParameter = empNo != null ?
                new ObjectParameter("empNo", empNo) :
                new ObjectParameter("empNo", typeof(string));
    
            return ((IObjectContextAdapter)this).ObjectContext.ExecuteFunction<Nullable<decimal>>("GetVacationDaysLeftProc", empNoParameter);
        }
    
        public virtual ObjectResult<GetOutStockBillsForAudit1To2_Result> GetOutStockBillsForAudit1To2(string account, string name)
        {
            var accountParameter = account != null ?
                new ObjectParameter("account", account) :
                new ObjectParameter("account", typeof(string));
    
            var nameParameter = name != null ?
                new ObjectParameter("name", name) :
                new ObjectParameter("name", typeof(string));
    
            return ((IObjectContextAdapter)this).ObjectContext.ExecuteFunction<GetOutStockBillsForAudit1To2_Result>("GetOutStockBillsForAudit1To2", accountParameter, nameParameter);
        }
    
        public virtual ObjectResult<GetSalaryInfo_new_Result> GetSalaryInfo_new(string salary_no)
        {
            var salary_noParameter = salary_no != null ?
                new ObjectParameter("salary_no", salary_no) :
                new ObjectParameter("salary_no", typeof(string));
    
            return ((IObjectContextAdapter)this).ObjectContext.ExecuteFunction<GetSalaryInfo_new_Result>("GetSalaryInfo_new", salary_noParameter);
        }
    
        public virtual ObjectResult<string> GetSalaryMonths(string salary_no)
        {
            var salary_noParameter = salary_no != null ?
                new ObjectParameter("salary_no", salary_no) :
                new ObjectParameter("salary_no", typeof(string));
    
            return ((IObjectContextAdapter)this).ObjectContext.ExecuteFunction<string>("GetSalaryMonths", salary_noParameter);
        }
    
        public virtual ObjectResult<GetSalarySummary_Result> GetSalarySummary(string salary_no, Nullable<System.DateTime> begin_date, Nullable<System.DateTime> end_date, string month_no)
        {
            var salary_noParameter = salary_no != null ?
                new ObjectParameter("salary_no", salary_no) :
                new ObjectParameter("salary_no", typeof(string));
    
            var begin_dateParameter = begin_date.HasValue ?
                new ObjectParameter("begin_date", begin_date) :
                new ObjectParameter("begin_date", typeof(System.DateTime));
    
            var end_dateParameter = end_date.HasValue ?
                new ObjectParameter("end_date", end_date) :
                new ObjectParameter("end_date", typeof(System.DateTime));
    
            var month_noParameter = month_no != null ?
                new ObjectParameter("month_no", month_no) :
                new ObjectParameter("month_no", typeof(string));
    
            return ((IObjectContextAdapter)this).ObjectContext.ExecuteFunction<GetSalarySummary_Result>("GetSalarySummary", salary_noParameter, begin_dateParameter, end_dateParameter, month_noParameter);
        }
    
        public virtual ObjectResult<GetSalaryAllDetail_Result> GetSalaryAllDetail(string salary_no, Nullable<System.DateTime> begin_date, Nullable<System.DateTime> end_date, string month_no)
        {
            var salary_noParameter = salary_no != null ?
                new ObjectParameter("salary_no", salary_no) :
                new ObjectParameter("salary_no", typeof(string));
    
            var begin_dateParameter = begin_date.HasValue ?
                new ObjectParameter("begin_date", begin_date) :
                new ObjectParameter("begin_date", typeof(System.DateTime));
    
            var end_dateParameter = end_date.HasValue ?
                new ObjectParameter("end_date", end_date) :
                new ObjectParameter("end_date", typeof(System.DateTime));
    
            var month_noParameter = month_no != null ?
                new ObjectParameter("month_no", month_no) :
                new ObjectParameter("month_no", typeof(string));
    
            return ((IObjectContextAdapter)this).ObjectContext.ExecuteFunction<GetSalaryAllDetail_Result>("GetSalaryAllDetail", salary_noParameter, begin_dateParameter, end_dateParameter, month_noParameter);
        }
    
        public virtual ObjectResult<GetSRFlowRecord_Result> GetSRFlowRecord(string account, Nullable<System.DateTime> beginDate, Nullable<System.DateTime> endDate)
        {
            var accountParameter = account != null ?
                new ObjectParameter("account", account) :
                new ObjectParameter("account", typeof(string));
    
            var beginDateParameter = beginDate.HasValue ?
                new ObjectParameter("beginDate", beginDate) :
                new ObjectParameter("beginDate", typeof(System.DateTime));
    
            var endDateParameter = endDate.HasValue ?
                new ObjectParameter("endDate", endDate) :
                new ObjectParameter("endDate", typeof(System.DateTime));
    
            return ((IObjectContextAdapter)this).ObjectContext.ExecuteFunction<GetSRFlowRecord_Result>("GetSRFlowRecord", accountParameter, beginDateParameter, endDateParameter);
        }
    
        public virtual ObjectResult<string> GetK3CustomerNameByNum(string customer_number, string company)
        {
            var customer_numberParameter = customer_number != null ?
                new ObjectParameter("customer_number", customer_number) :
                new ObjectParameter("customer_number", typeof(string));
    
            var companyParameter = company != null ?
                new ObjectParameter("company", company) :
                new ObjectParameter("company", typeof(string));
    
            return ((IObjectContextAdapter)this).ObjectContext.ExecuteFunction<string>("GetK3CustomerNameByNum", customer_numberParameter, companyParameter);
        }
    
        public virtual ObjectResult<GetK3Item_Result> GetK3Item(string account, string item_no)
        {
            var accountParameter = account != null ?
                new ObjectParameter("account", account) :
                new ObjectParameter("account", typeof(string));
    
            var item_noParameter = item_no != null ?
                new ObjectParameter("item_no", item_no) :
                new ObjectParameter("item_no", typeof(string));
    
            return ((IObjectContextAdapter)this).ObjectContext.ExecuteFunction<GetK3Item_Result>("GetK3Item", accountParameter, item_noParameter);
        }
    
        public virtual ObjectResult<GetApBusAndDepNamesInK3_Result> GetApBusAndDepNamesInK3(string account)
        {
            var accountParameter = account != null ?
                new ObjectParameter("account", account) :
                new ObjectParameter("account", typeof(string));
    
            return ((IObjectContextAdapter)this).ObjectContext.ExecuteFunction<GetApBusAndDepNamesInK3_Result>("GetApBusAndDepNamesInK3", accountParameter);
        }
    
        public virtual ObjectResult<GetItemStockQtyFromK3_Result> GetItemStockQtyFromK3(string item_no)
        {
            var item_noParameter = item_no != null ?
                new ObjectParameter("item_no", item_no) :
                new ObjectParameter("item_no", typeof(string));
    
            return ((IObjectContextAdapter)this).ObjectContext.ExecuteFunction<GetItemStockQtyFromK3_Result>("GetItemStockQtyFromK3", item_noParameter);
        }
    
        public virtual ObjectResult<GetAPHistoryQty_Result> GetAPHistoryQty(string item_number, string bus_name, Nullable<System.DateTime> begin_date, Nullable<System.DateTime> end_date)
        {
            var item_numberParameter = item_number != null ?
                new ObjectParameter("item_number", item_number) :
                new ObjectParameter("item_number", typeof(string));
    
            var bus_nameParameter = bus_name != null ?
                new ObjectParameter("bus_name", bus_name) :
                new ObjectParameter("bus_name", typeof(string));
    
            var begin_dateParameter = begin_date.HasValue ?
                new ObjectParameter("begin_date", begin_date) :
                new ObjectParameter("begin_date", typeof(System.DateTime));
    
            var end_dateParameter = end_date.HasValue ?
                new ObjectParameter("end_date", end_date) :
                new ObjectParameter("end_date", typeof(System.DateTime));
    
            return ((IObjectContextAdapter)this).ObjectContext.ExecuteFunction<GetAPHistoryQty_Result>("GetAPHistoryQty", item_numberParameter, bus_nameParameter, begin_dateParameter, end_dateParameter);
        }
    
        public virtual ObjectResult<GetAPPriceHistory_Result> GetAPPriceHistory(string item_no)
        {
            var item_noParameter = item_no != null ?
                new ObjectParameter("item_no", item_no) :
                new ObjectParameter("item_no", typeof(string));
    
            return ((IObjectContextAdapter)this).ObjectContext.ExecuteFunction<GetAPPriceHistory_Result>("GetAPPriceHistory", item_noParameter);
        }
    
        public virtual ObjectResult<GetHREmpInfoDetail_Result> GetHREmpInfoDetail(string card_no)
        {
            var card_noParameter = card_no != null ?
                new ObjectParameter("card_no", card_no) :
                new ObjectParameter("card_no", typeof(string));
    
            return ((IObjectContextAdapter)this).ObjectContext.ExecuteFunction<GetHREmpInfoDetail_Result>("GetHREmpInfoDetail", card_noParameter);
        }
    
        public virtual int UpdateHrEmpDept(string card_number, Nullable<int> dep_id, Nullable<System.DateTime> in_time, string position)
        {
            var card_numberParameter = card_number != null ?
                new ObjectParameter("card_number", card_number) :
                new ObjectParameter("card_number", typeof(string));
    
            var dep_idParameter = dep_id.HasValue ?
                new ObjectParameter("dep_id", dep_id) :
                new ObjectParameter("dep_id", typeof(int));
    
            var in_timeParameter = in_time.HasValue ?
                new ObjectParameter("in_time", in_time) :
                new ObjectParameter("in_time", typeof(System.DateTime));
    
            var positionParameter = position != null ?
                new ObjectParameter("position", position) :
                new ObjectParameter("position", typeof(string));
    
            return ((IObjectContextAdapter)this).ObjectContext.ExecuteFunction("UpdateHrEmpDept", card_numberParameter, dep_idParameter, in_timeParameter, positionParameter);
        }
    
        public virtual ObjectResult<GetKQRecord_Result> GetKQRecord(string account_no)
        {
            var account_noParameter = account_no != null ?
                new ObjectParameter("account_no", account_no) :
                new ObjectParameter("account_no", typeof(string));
    
            return ((IObjectContextAdapter)this).ObjectContext.ExecuteFunction<GetKQRecord_Result>("GetKQRecord", account_noParameter);
        }
    
        public virtual ObjectResult<byte[]> GetHREmpPortrait(string card_no)
        {
            var card_noParameter = card_no != null ?
                new ObjectParameter("card_no", card_no) :
                new ObjectParameter("card_no", typeof(string));
    
            return ((IObjectContextAdapter)this).ObjectContext.ExecuteFunction<byte[]>("GetHREmpPortrait", card_noParameter);
        }
    
        public virtual ObjectResult<GetHREmpInfo_Result> GetHREmpInfo(string card_no)
        {
            var card_noParameter = card_no != null ?
                new ObjectParameter("card_no", card_no) :
                new ObjectParameter("card_no", typeof(string));
    
            return ((IObjectContextAdapter)this).ObjectContext.ExecuteFunction<GetHREmpInfo_Result>("GetHREmpInfo", card_noParameter);
        }
    }
}
