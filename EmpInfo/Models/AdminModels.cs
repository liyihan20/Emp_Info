using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace EmpInfo.Models
{
    public class UserListModel
    {
        public string name { get; set; }
        public string cardNo { get; set; }
        public string status { get; set; }
        public string sex { get; set; }
        public string depName { get; set; }
    }

    public class GroupUserModel
    {
        public int? id { get; set; }
        public string cardNo { get; set; }
        public string userName { get; set; }
    }

    public class GroupAutModel
    {
        public int? id { get; set; }

        public decimal? autNumber { get; set; }
        public string autName { get; set; }

        public string autDes{get;set;}
    }

    public class Department
    {
        public string text { get; set; }
        public List<Department> nodes { get; set; }
        public string[] tags { get; set; }        
        public string color { get; set; }
        public bool selectable { get; set; }
    }

    public class BusPlaces
    {
        public int place_id { get; set; }
        public int sort_no { get; set; }
        public string place { get; set; }
        public string floor { get; set; }
        public string dep_name { get; set; }
        public int area_size { get; set; }
        public int dep_size { get; set; }
        public string clear_level { get; set; }
        public string dep_plan { get; set; }
        public string usage { get; set; }
        public string pic_name { get; set; }
        public int detail_id { get; set; }
        public string dep_charger { get; set; }
    }

    public class DSModel
    {
        public List<BusPlaces> ps { get; set; }
        public int isEmpty { get; set; }
        public string place { get; set; }
        public string depName { get; set; }
        public string depCharger { get; set; }
        public List<string> places { get; set; }
        public List<string> chargers { get; set; }
    }

}