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

}