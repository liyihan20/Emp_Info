using EmpInfo.FlowSvr;
using EmpInfo.Models;
using EmpInfo.Util;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;

namespace EmpInfo.Services
{
    /// <summary>
    /// 设备保养流程
    /// </summary>
    public class MTSv:BillSv
    {
        public ei_mtApply bill;
        public MTSv() { }
        public MTSv(string sysNo)
        {
            bill = db.ei_mtApply.Single(m => m.sys_no == sysNo);
        }
        public override string BillType
        {
            get { return "MT"; }
        }

        public override string BillTypeName
        {
            get { return "设备保养单"; }
        }

        public override List<ApplyMenuItemModel> GetApplyMenuItems(UserInfo userInfo)
        {
            var menus = new List<ApplyMenuItemModel>();

            menus.Add(new ApplyMenuItemModel()
            {
                url = "GetMyApplyList?billType=" + BillType,
                text = "我申请的",
                iconFont = "fa-th"
            });
            menus.Add(new ApplyMenuItemModel()
            {
                url = "GetMyAuditingList?billType=" + BillType,
                text = "我的待办",
                iconFont = "fa-th-list"
            });
            menus.Add(new ApplyMenuItemModel()
            {
                url = "GetMyAuditedList?billType=" + BillType,
                text = "我的已办",
                iconFont = "fa-th-large"
            });

            if (db.ei_flowAuthority.Where(f => f.bill_type == BillType && f.relate_type == "科室与保养文件" && f.relate_value == userInfo.cardNo).Count() > 0) {
                menus.Add(new ApplyMenuItemModel()
                {
                    text = "设备信息维护",
                    url = "../ApplyExtra/CheckMTEqInfo",
                    iconFont = "fa-cube",
                    colorClass = "text-danger"
                });

                menus.Add(new ApplyMenuItemModel()
                {
                    text = "设备保养文件维护",
                    url = "../ApplyExtra/CheckMTFiles",
                    iconFont = "fa-file-archive-o"
                });

                menus.Add(new ApplyMenuItemModel()
                {
                    text = "设备科室维护",
                    url = "../ApplyExtra/CheckMTClasses",
                    iconFont = "fa-user-circle"
                });

                menus.Add(new ApplyMenuItemModel()
                {
                    text = "查询报表",
                    iconFont = "fa-file-text-o",
                    url = "../Report/MTReport",
                });
            }

            return menus;
        }

        public override object GetInfoBeforeApply(UserInfo userInfo, UserInfoDetail userInfoDetail)
        {
            throw new NotImplementedException();
        }

        public override void SaveApply(System.Web.Mvc.FormCollection fc, UserInfo userInfo)
        {
            throw new NotImplementedException();
        }

        public override object GetBill()
        {
            var m = (from a in db.ei_mtApply
                     join i in db.ei_mtEqInfo on a.eqInfo_id equals i.id
                     join f in db.ei_mtFile on i.file_no equals f.file_no
                     join c in db.ei_mtClass on i.class_id equals c.id
                     where a.sys_no == bill.sys_no
                     select new MTBillModel()
                     {
                         apply = a,
                         eqInfo = i,
                         file = f,
                         cla = c
                     }).FirstOrDefault();

            return m;
        }

        public override string AuditViewName()
        {
            return "BeginAuditMTApply";
        }

        public override SimpleResultModel HandleApply(System.Web.Mvc.FormCollection fc, UserInfo userInfo)
        {
            int step = Int32.Parse(fc.Get("step"));
            string stepName = fc.Get("stepName");

            var eqInfo = db.ei_mtEqInfo.Single(e => e.id == bill.eqInfo_id);
            if (stepName.Contains("接单")) {
                if (bill.accept_time != null) {
                    throw new Exception("操作失败：此保养单刚已被其他同事接单");
                }

                bill.accept_time = DateTime.Now;
                bill.accept_member_no = userInfo.cardNo;
                bill.accept_member_name = userInfo.name;

                eqInfo.maintenance_status = "正在保养";

            }
            else if (stepName.Contains("处理")) {
                MyUtils.SetFieldValueToModel(fc, bill);
                bill.maintence_time = DateTime.Now;
                bill.maintence_hours = (decimal)Math.Round((((DateTime)bill.maintence_end_time) - ((DateTime)bill.maintence_begin_time)).TotalMinutes / 60.0, 1);
            }
            else if (stepName.Contains("确认")) {
                bill.confirm_time = DateTime.Now;
                
                eqInfo.maintenance_status = "完成保养";
                eqInfo.last_maintenance_date = bill.maintence_end_time;
                eqInfo.next_maintenance_date = ((DateTime)bill.maintence_end_time).AddMonths(eqInfo.maintenance_cycle);
            }
            
            FlowSvrSoapClient flow = new FlowSvrSoapClient();
            var result = flow.BeginAudit(bill.sys_no, step, userInfo.cardNo, true, "", JsonConvert.SerializeObject(bill));
            if (result.suc) {
                db.SaveChanges();
                //发送通知到下一级审核人
                SendNotification(result);
            }
            return new SimpleResultModel() { suc = result.suc, msg = result.msg };
        }

        public override void SendNotification(FlowResultModel model)
        {
            if (model.suc) {
                if (model.msg.Contains("完成") || model.msg.Contains("NG")) {
                    
                }
                else {
                    FlowSvrSoapClient flow = new FlowSvrSoapClient();
                    var result = flow.GetCurrentStep(bill.sys_no);
                    var eqInfo = db.ei_mtEqInfo.Single(e => e.id == bill.eqInfo_id);

                    SendEmailToNextAuditor(
                        bill.sys_no,
                        result.step,
                        string.Format("你有一张待审批的{0}", BillTypeName),
                        GetUserNameByCardNum(model.nextAuditors),
                        string.Format("你有一张待处理的单号为【{0}】的{1}，请尽快登陆系统处理。", bill.sys_no, BillTypeName),
                        GetUserEmailByCardNum(model.nextAuditors)
                        );

                    string[] nextAuditors = model.nextAuditors.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);

                    SendQywxMessageToNextAuditor(
                        BillTypeName,
                        bill.sys_no,
                        result.step,
                        result.stepName,
                        bill.applier_name,
                        ((DateTime)bill.apply_time).ToString("yyyy-MM-dd HH:mm"),
                        string.Format("生产车间：{0}；设备名称：{1}；", eqInfo.produce_dep_name, eqInfo.equitment_name),
                        nextAuditors.ToList()
                        );

                }
            }
        }

        #region 保养文件维护

        public object CheckFiles(){
            var files = (from f in db.ei_mtFile
                         select new
                         {
                             id = f.id,
                             file_no = f.file_no,
                             create_user = f.create_user,
                             content = f.maintenance_content.Length > 20 ? f.maintenance_content.Substring(0, 20) : f.maintenance_content,
                             steps = f.maintenance_steps.Length > 20 ? f.maintenance_steps.Substring(0, 20) : f.maintenance_steps,
                             update_user = f.update_user
                         }).ToList();
            return files;
        }

        public ei_mtFile GetFile(int fileId)
        {
            var file = db.ei_mtFile.Where(f => f.id == fileId).FirstOrDefault();
            if (file == null) {
                throw new Exception("保养文件不存在");
            }
            return file;
        }

        public void SaveFile(int id,string fileNo, string content, string steps,string userName)
        {
            if (db.ei_mtFile.Where(f => f.id != id && f.file_no == fileNo).Count() > 0) {
                throw new Exception("保养文件编号和之前的重复，不能保存");
            }

            ei_mtFile file;
            if (id == 0) {
                file = new ei_mtFile();
                file.create_user = userName;
                file.create_time = DateTime.Now;
                db.ei_mtFile.Add(file);
            }
            else {
                file = db.ei_mtFile.Where(f => f.id == id).FirstOrDefault();
                if (file == null) {
                    throw new Exception("修改的文件id不存在");
                }
            }
            
            file.file_no = fileNo;
            file.maintenance_content = content;
            file.maintenance_steps = steps;
            file.update_time = DateTime.Now;
            file.update_user = userName;

            db.SaveChanges();
        }

        public void RemoveFile(int id)
        {
            var file = db.ei_mtFile.Where(f => f.id == id).FirstOrDefault();
            if (db.ei_mtEqInfo.Where(m => m.file_no == file.file_no).Count() > 0) {
                throw new Exception("存在关联此保养文件的设备，不能删除");
            }
            db.ei_mtFile.Remove(file);
            db.SaveChanges();
        }        

        #endregion

        #region 设备科室维护

        public List<ei_mtClass> GetClasses()
        {
            return db.ei_mtClass.ToList();
        }

        public void SaveClass(ei_mtClass cla)
        {
            cla.leader_number = GetUserCardByNameAndCardNum(cla.leader);
            cla.member_number = GetUserCardByNameAndCardNum(cla.member);

            if (cla.id == 0) {
                cla.create_time = DateTime.Now;
                db.ei_mtClass.Add(cla);
            }
            else {
                ei_mtClass existedCla = db.ei_mtClass.Where(c => c.id == cla.id).FirstOrDefault();
                if (existedCla == null) {
                    throw new Exception("当前设备科室不存在");
                }
                existedCla.leader = cla.leader;
                existedCla.leader_number = cla.leader_number;
                existedCla.member = cla.member;
                existedCla.member_number = cla.member_number;
                existedCla.class_name = cla.class_name;
            }

            db.SaveChanges();
        }

        public void RemoveClass(int id)
        {
            if (db.ei_mtEqInfo.Where(m => m.class_id == id).Count() > 0) {
                throw new Exception("不能删除，当前科室存在保养设备");
            }

            db.ei_mtClass.Remove(db.ei_mtClass.Where(c => c.id == id).FirstOrDefault());
            db.SaveChanges();
        }

        #endregion

        #region 设备资料维护

        //获取当前用户对应的设备科室id
        public string GetMyClassId(string userNumber)
        {
            int classId = db.ei_mtClass.Where(m => m.leader_number.Contains(userNumber)).Select(m => m.id).FirstOrDefault();
            if (classId == 0) return "";
            return classId.ToString();
        }

        //获取所有的科室选项给前台select控件
        public List<SelectModel> GetClassesForSelect()
        {
            return db.ei_mtClass.Select(m => new SelectModel() { text = m.class_name, intValue = m.id }).ToList();
        }

        //获取所有的生产车间
        public List<vw_ep_dep> GetEpDepList()
        {
            return db.vw_ep_dep.OrderBy(e => e.pr_dep_name).ToList();
        }

        //获取所有的保养文件编号
        public List<string> GetFileNoList()
        {
            return db.ei_mtFile.OrderBy(f => f.file_no).Select(f => f.file_no).ToList();
        }

        //通过编号获取保养文件明细
        public ei_mtFile GetFileDetail(string fileNo)
        {
            return db.ei_mtFile.Where(f => f.file_no == fileNo).FirstOrDefault();
        }

        //获取当前用户负责的科室对应的设备资料
        public object GetEqInfoList(string userNumber)
        {
            bool canSeeAll = db.ei_flowAuthority.Where(f => f.bill_type == BillType && f.relate_type == "查看所有设备" && f.relate_value == userNumber).Count() > 0;
            var list = (from eq in db.ei_mtEqInfo
                          join c in db.ei_mtClass on eq.class_id equals c.id
                          where c.leader_number.Contains(userNumber) || eq.creater_number == userNumber || canSeeAll
                          orderby eq.maintenance_status
                          select new
                          {
                              eq.property_number,
                              eq.equitment_name,
                              eq.equitment_modual,
                              eq.important_level,
                              eq.next_maintenance_date,
                              eq.maintenance_status,
                              eq.produce_dep_name,
                              eq.id,
                              eq.creater_name
                          }).ToList();
            var result = list.Where(l => l.maintenance_status == "正在保养").OrderBy(l => l.next_maintenance_date).ToList();
            result.AddRange(list.Where(l => l.maintenance_status != "正在保养").OrderBy(l => l.maintenance_status).ThenBy(l => l.next_maintenance_date).ToList());

            return result;
        }

        //查看设备资料明细
        public object GetEqInfoDetail(int id)
        {
            var result = from eq in db.ei_mtEqInfo
                         join c in db.ei_mtClass on eq.class_id equals c.id
                         join f in db.ei_mtFile on eq.file_no equals f.file_no
                         join td in db.vw_ep_dep on eq.produce_dep_name equals td.pr_dep_name into tempd
                         from t in tempd.DefaultIfEmpty()
                         where eq.id == id
                         select new
                         {
                             eq.id,
                             eq.check_dep,
                             eq.check_list_no,
                             eq.comment,
                             eq.create_time,
                             eq.creater_name,
                             eq.equitment_modual,
                             eq.equitment_name,
                             eq.file_no,
                             eq.important_level,
                             eq.last_maintenance_date,
                             eq.maintenance_cycle,
                             eq.maintenance_status,
                             eq.maker,
                             eq.next_maintenance_date,
                             eq.produce_dep_name,
                             eq.property_number,
                             eq.using_status,
                             eq.class_id,
                             c.leader,
                             c.class_name,                             
                             f.maintenance_content,
                             f.maintenance_steps,
                             t.bus_dep_name,
                             t.eq_charger_name,
                             t.pr_dep_name,
                             t.pr_charger_name,
                             t.eq_dep_name
                         };
            return result.FirstOrDefault();
        }

        public void SaveEqInfo(ei_mtEqInfo info,UserInfo userInfo)
        {
            if (db.ei_mtEqInfo.Where(m => m.id != info.id && m.property_number == info.property_number).Count() > 0) {
                throw new Exception("资产编号不能重复");
            }
            if (info.id == 0) {
                info.create_time = DateTime.Now;
                info.creater_name = userInfo.name;
                info.creater_number = userInfo.cardNo;
                info.maintenance_status = "等待执行";                
                db.ei_mtEqInfo.Add(info);
            }
            else {
                var toModifyedInfo = db.ei_mtEqInfo.Where(e => e.id == info.id).FirstOrDefault();
                if (toModifyedInfo == null) {
                    throw new Exception("设备资料不存在");
                }
                info.create_time = toModifyedInfo.create_time; //这个要单独设置一下才能保存成功
                MyUtils.CopyPropertyValue(info, toModifyedInfo);
            }
            db.SaveChanges();
        }

        public void RemoveEqInfo(int id)
        {
            if (db.ei_mtApply.Where(m => m.eqInfo_id == id).Count() > 0) {
                throw new Exception("已有关联的保养申请单，不能删除。");
            }
            db.ei_mtEqInfo.Remove(db.ei_mtEqInfo.Where(e => e.id == id).FirstOrDefault());
            db.SaveChanges();
        }

        public object GetApplyRecord(int eqInfoId)
        {
            var result = (from m in db.ei_mtApply
                          where m.eqInfo_id == eqInfoId
                          orderby m.apply_time descending
                          select new
                          {
                              m.accept_member_name,
                              m.sys_no,
                              m.maintence_begin_time,
                              m.maintence_end_time
                          }).ToList();
            return result;
        }

        #endregion

    }
}