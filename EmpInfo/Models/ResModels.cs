using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace EmpInfo.Models
{
    public class SimpleDishModel
    {        
        public int id { get; set; }
        public string type { get; set; }
        public string name { get; set; }
        public decimal? price { get; set; }    
        public string sell_weekday { get; set; }
        public string sell_time { get; set; }
        public bool? is_on_top { get; set; }
    }

    public class DishDetailModel
    {
        public int id { get; set; }
        public string number { get; set; }
        public string type { get; set; }
        public string name { get; set; }
        public decimal? price { get; set; }
        public string sell_weekday { get; set; }
        public string sell_time { get; set; }
        public bool? is_on_top { get; set; }
        public bool? can_delivery { get; set; }
        public string description { get; set; }
        public bool? is_selling { get; set; }
        public bool has_image_1 { get; set; }
        public bool has_image_2 { get; set; }
        public bool has_image_3 { get; set; }
    }

    public class ShoppingCarModel
    {
        public int car_id { get; set; }
        public int? dish_id { get; set; }
        public string name { get; set; }
        public decimal? price { get; set; }
        public int? qty { get; set; }
        public bool? is_selling { get; set; }
        public bool? is_checked { get; set; }
        public bool? can_delivery { get; set; }
        public string sell_day { get; set; }
        public string sell_time { get; set; }
        public string benefit_info { get; set; }

    }

    public class PointsRecordModel
    {
        public int income { get; set; }
        public string info { get; set; }
        public string date { get; set; }
    }

    public class PointsForDishModel
    {
        public int id { get; set; }
        public int? dishId { get; set; }
        public string dishName { get; set; }
        public int pointsNeed { get; set; }
        public int? hasFullfill { get; set; }
        public string fromDate { get; set; }
        public string endDate { get; set; }
    }

    public class BirthdayMealModel
    {
        public string birthday { get; set; }
        public int hasGotTimes { get; set; }
        public bool thisYearHasGot { get; set; }
        public bool nowCanGetMeal { get; set; }
        public List<birthDishModel> dishList { get; set; }
    }

    public class birthDishModel
    {
        public int dishId { get; set; }
        public string dishName { get; set; }
        public int hasGotNum { get; set; }
    }

    public enum WeekDay
    {
        周一, 周二, 周三, 周四, 周五, 周六, 周日
    }

    public enum MealSegment
    {
        早餐, 午餐, 晚餐, 宵夜
    }

    //食堂和菜式总量模型，用于选择食堂的时候显示在按钮上
    public class ResAndDishCountModel
    {
        public string resNo { get; set; }
        public string resName { get; set; }
        public int dishCount { get; set; }
    }

    //台桌信息模型，某区有多少行多少列台桌,用于可视化选桌
    public class DeskInfoModel
    {
        public string belongTo { get; set; }
        public string zone { get; set; }
        public int maxRow { get; set; }
        public int maxCol { get; set; }
    }

    //台桌模型，用于可视化选卓
    public class VisualDeskModel
    {
        //编号
        public string number { get; set; }
        //名称
        public string name { get; set; }
        //属于大堂或包间
        public string belongTo { get; set; }
        //区域
        public string zone { get; set; }
        //可坐数量
        public int? seatQty { get; set; }
        //是否作废
        public bool? isCancel { get; set; }
        //已占用数量
        public int? seatHasTaken { get; set; }
        //当前是否可用
        public bool nowCanUse { get; set; }
    }

    public class ResConsumeData{
        public string name { get; set; }
        public decimal? value { get; set; } 
    }

}