using EmpInfo.Models;
using EmpInfo.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace EmpInfo.Services
{
    /// <summary>
    /// 与人事系统有关的DB操作
    /// </summary>
    public class HRDBSv:BaseSv
    {
        //获取考勤打卡记录
        public List<GetKQRecord_Result> GetKQRecored(string account)
        {
            return db.GetKQRecord(account).ToList();
        }

        //是否在职
        public bool EmpIsNotQuit(string cardNumber)
        {
            if (cardNumber.ToUpper().StartsWith("GN")) {
                return true; //光能办
            }
            try {
                foreach (var cn in cardNumber.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries)) {
                    if (db.GetHREmpInfo(cn).Count() == 0) {
                        return false;
                    }
                }                
            }
            catch {
                return true; //人事系统连接不失败就当做是在职
            }
            return true;
        }

        // 获取人事系统信息
        public GetHREmpInfo_Result GetHREmpInfo(string cardNumber)
        {
            return db.GetHREmpInfo(cardNumber).FirstOrDefault();
        }

        public GetHREmpInfoDetail_Result GetHREmpDetailInfo(string cardNumber)
        {
            return db.GetHREmpInfoDetail(cardNumber).FirstOrDefault();
        }

        public byte[] GetHREmpPortrait(string cardNumber)
        {
            try {
                var result = db.GetHREmpPortrait(cardNumber).FirstOrDefault();
                if (result == null) return null;
                return MyUtils.MakeThumbnail(MyUtils.BytesToImage(result));
            }
            catch {
                return null;
            }  
        }
        

    }



}