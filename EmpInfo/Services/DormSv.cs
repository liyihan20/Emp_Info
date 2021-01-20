using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using EmpInfo.Models;
using Newtonsoft.Json;

namespace EmpInfo.Services
{
    public class DormSv:BaseSv
    {
        DormDBDataContext dorm = new DormDBDataContext();

        #region 厂内住宿员工的方法

        public DormRepairBeginApplyModel getBeginApplyModel(string applierNumber)
        {
            var m = (from d in dorm.dormitory_dorm
                     join l in dorm.dormitory_lodging on d.id equals l.dorm_id
                     join e in dorm.dormitory_employee on l.emp_id equals e.id
                     join a in dorm.dormitory_area on d.area_number equals a.id
                     where l.out_date == null
                     select new DormRepairBeginApplyModel()
                     {
                         areaName = a.name,
                         dormNumber = d.number
                     }).FirstOrDefault();

            if (m != null) {
                var roommates = (from d in dorm.dormitory_dorm
                                 join l in dorm.dormitory_lodging on d.id equals l.dorm_id
                                 join e in dorm.dormitory_employee on l.emp_id equals e.id
                                 where d.number == m.dormNumber && e.card_number != applierNumber
                                 select new {e.card_number,e.name}).ToList();
                if (roommates.Count() == 0) {
                    m.roomMaleList = "";
                }
                else {
                    foreach (var roommate in roommates) {
                        m.roomMaleList += roommate.card_number + ":" + roommate.name + ";";
                    }
                }
            }

            return m;
        }

        /// <summary>
        /// 宿舍系统员工表的id，排除7天内新入住的，因为新入住不用扣费
        /// </summary>
        /// <param name="applierNumber"></param>
        /// <param name="roommatesNumber"></param>
        /// <returns></returns>
        public string GetEmpIdShouldPay(DateTime applyTime, string applierNumber, string roommatesNumber)
        {
            List<string> empNumberList = new List<string>() { applierNumber };
            if (!string.IsNullOrEmpty(roommatesNumber)) {
                var roommates = roommatesNumber.Split(new char[] { ';' },StringSplitOptions.RemoveEmptyEntries).ToList();
                empNumberList.AddRange(roommates);
            }

            applyTime = applyTime.AddDays(-7);

            var result = (from em in dorm.dormitory_employee
                          join l in dorm.dormitory_lodging on em.id equals l.emp_id
                          where l.out_date == null && empNumberList.Contains(em.card_number)
                          && l.in_date < applyTime
                          select new
                          {
                              empId = em.id
                          }).ToList();

            var empIds = "";
            foreach (var re in result) {
                empIds += re.empId + ";";
            }

            return empIds;
        }

        /// <summary>
        /// 选择自己扣款时，验证是否新入住员工，如果是，看是否有其他舍友，如果有其他舍友的必须选择分摊
        /// </summary>
        /// <param name="dormNumber"></param>
        /// <returns></returns>
        public bool validateWhileSelfPay(DateTime applyTime, string dormNumber,string applierNumber)
        {
            applyTime = applyTime.AddDays(-7);

            var result = (from em in dorm.dormitory_employee
                          join l in dorm.dormitory_lodging on em.id equals l.emp_id
                          where l.out_date == null && l.in_date > applyTime
                          && em.card_number == applierNumber
                          select l.dorm_id).FirstOrDefault();

            if (result != null) {
                //是新入住
                if (dorm.dormitory_lodging.Where(l => l.dorm_id == result && l.out_date == null).Count() > 1) {
                    return false;
                }
            }
            return true;
        }


        #endregion

        #region 厂外员工维修

        public void validateDPOutside(ei_dormRepair dr)
        {
            //先查看宿舍是否存在
            var v1 = (from d in dorm.dormitory_dorm
                      join a in dorm.dormitory_area on d.area_number equals a.id
                      join l in dorm.dormitory_lodging on d.id equals l.dorm_id
                      join e in dorm.dormitory_employee on l.emp_id equals e.id
                      where d.number == dr.dorm_num
                      && a.name == dr.area_name
                      && l.out_date == null
                      && (e.card_number == null || e.card_number == "" || e.account_number == null || e.account_number == "")
                      select new { dormId = d.id, empId = l.emp_id, empName = e.name, inDate = l.in_date }).ToList();

            if (v1.Count() == 0) {
                throw new Exception("宿舍信息验证失败，可能原因：1. 宿舍区与宿舍号不匹配；2.此宿舍当前没有厂外在住人员");
            }
            var sevenDaysAgo = ((DateTime)dr.apply_time).AddDays(-7);
            if (v1.Count() == 1) {
                dr.fee_share_type = "户主本人";
                if (sevenDaysAgo < v1.First().inDate) {
                    //7天内入住
                    dr.emp_id_should_pay = "";
                }
                else {
                    dr.emp_id_should_pay = v1.First().empId.ToString();
                }
            }
            if (v1.Count() > 1) {
                //多人宿舍
                if (v1.Where(v => v.empName.StartsWith(dr.applier_name)).Count() == 0) {
                    throw new Exception("宿舍信息验证失败，原因：此多人宿舍没有当前联系人的在住信息,请检查联系人姓名是否正确");
                }

                dr.fee_share_type = "舍友分摊";
                dr.emp_id_should_pay = "";
                var v2 = v1.Where(v => v.inDate < sevenDaysAgo).ToList();
                foreach (var v in v2) {
                    dr.fee_share_peple = (dr.fee_share_peple ?? "") + v.empName + ";";
                    dr.emp_id_should_pay += v.empId + ";";
                }
            }

        }

        #endregion

    }
}