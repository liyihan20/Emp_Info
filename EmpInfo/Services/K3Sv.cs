using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using EmpInfo.Models;

namespace EmpInfo.Services
{
    public class K3Sv:BaseSv
    {
        private string _accountName;
        public K3Sv() { }
        public K3Sv(string accountName)
        {
            _accountName = accountName;
        }
        public string GetConnStr()
        {
            if (string.IsNullOrEmpty(_accountName)) throw new Exception("没有K3帐套信息");
            var a = db.k3_database.Where(k => k.account_name == _accountName).FirstOrDefault();
            if (a == null) throw new Exception("没有数据库连接：" + _accountName);

            return string.Format("[{0}].[{1}]", a.server_ip, a.database_name);
        }

        public List<K3Product> GetK3ProductByInfo(string itemInfo)
        {
            string sql = @"select top 50 t1.FItemID as item_id,t1.Fmodel as item_model,t1.FName as item_name,
                         t1.FNumber as item_no,t2.FName as unit_name
                         from {db}.dbo.t_ICItem t1 
                         left join {db}.dbo.t_MeasureUnit t2 on t1.FSaleUnitID = t2.fitemid 
                         inner join {db}.dbo.t_item t3 on t1.FItemID = t3.FItemID and t3.FDeleted = 0 
                         where t1.FDeleted = 0 and (t1.FModel like {0} or t1.FName like {0} or t1.FNumber = {0})";
            sql = sql.Replace("{db}", GetConnStr());

            return db.Database.SqlQuery<K3Product>(sql, "%" + itemInfo + "%").ToList();
        }

        public int GetK3ProductIdByNumber(string productNumber)
        {
            string sql = @"select FItemID from {db}.dbo.t_ICItem where FNumber = {0}";
            sql = sql.Replace("{db}", GetConnStr());

            return db.Database.SqlQuery<int>(sql, productNumber).FirstOrDefault();
        }

        public List<K3BomInfo> GetK3BomInfo(string productNo)
        {
            string sql = @"select 'bom' as sour, t2.FItemID as item_id,t4.FName as item_name,t4.FModel as item_model,
                        t4.FNumber as item_no,CONVERT(decimal(18,6), t2.FAuxQty) as per_qty, t3.FName as unit_name
                        from {db}.dbo.ICBOM t1 
                        inner join {db}.dbo.ICBOMChild t2 on t1.FInterID = t2.FInterID
                        inner join {db}.dbo.t_MeasureUnit t3 on t2.FUnitID = t3.FItemID
                        inner join {db}.dbo.t_ICItem t4 on t2.FItemID = t4.FItemID
                        inner join {db}.dbo.t_ICItem t5 on t1.FItemID = t5.FItemID
                        where t1.FUseStatus = 1072 and t5.FNumber = {0}";
            sql = sql.Replace("{db}", GetConnStr());

            return db.Database.SqlQuery<K3BomInfo>(sql, productNo).ToList();
        }

        //获取事业部的采购订单信息
        public List<POInfoModel> GetK3BusPO(string poNumber)
        {
            string sql = "";
            if (new string[] { "光电仁寿", "光电科技", "电子", "仪器", "工业" }.Contains(_accountName)) {
                sql = @"select t2.fentryid as entry_id,t2.fitemid as item_id,t3.fname as item_name,
                        t3.fnumber as item_no,t3.fmodel as item_modual,t2.fauxqty as qty,t4.fname as unit_name
                        from {db}.dbo.PORequest t1
                        inner join {db}.dbo.PORequestentry t2 on t1.finterid = t2.finterid
                        inner join {db}.dbo.t_icitem t3 on t2.fitemid = t3.fitemid
                        inner join {db}.dbo.t_MeasureUnit t4 on t2.FUnitID = t4.FItemID
                        where t1.fcancellation = 0 and t1.FBillNo = {0}";
            }
            else {
                sql = @"select t2.fentryid as entry_id,t2.fitemid as item_id,t3.fname as item_name,
                        t3.fnumber as item_no,t3.fmodel as item_modual,t2.fauxqty as qty,t4.fname as unit_name
                        from {db}.dbo.poorder t1
                        inner join {db}.dbo.poorderentry t2 on t1.finterid = t2.finterid
                        inner join {db}.dbo.t_icitem t3 on t2.fitemid = t3.fitemid
                        inner join {db}.dbo.t_MeasureUnit t4 on t2.FUnitID = t4.FItemID
                        where t1.fcancellation = 0 and t1.FBillNo = {0}";
            }
            sql = sql.Replace("{db}", GetConnStr());

            return db.Database.SqlQuery<POInfoModel>(sql, poNumber).ToList();
        }

        //获取事业部的委外加工出库单信息
        public List<POInfoModel> GetK3BusStockBill(string billNumber)
        {
            string sql = @"select t3.fname as item_name,t3.fmodel as item_modual,t2.FAuxQty as qty,t4.FName as unit_name
                        from {db}.dbo.ICStockBill t1
                        inner join {db}.dbo.ICStockBillEntry t2 on t1.finterid = t2.finterid
                        inner join {db}.dbo.t_icitem t3 on t2.fitemid = t3.fitemid
                        inner join {db}.dbo.t_MeasureUnit t4 on t2.FUnitID = t4.FItemID
                        where t1.FTranType = 28 and t1.FCancellation = 0 and t1.FBillNo = {0}";
            sql = sql.Replace("{db}", GetConnStr());

            var list = db.Database.SqlQuery<POInfoModel>(sql, billNumber).ToList();

            return list;
        }

        //搜索所有总部帐套的供应商信息
        public List<K3AccountModel> SearchK3Supplier(string searchValue)
        {
            return db.Database.SqlQuery<K3AccountModel>("exec SearchK3Suppier @searchValue = {0}", searchValue).ToList();
        }

    }
}