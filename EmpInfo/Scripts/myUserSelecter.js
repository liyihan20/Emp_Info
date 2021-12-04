(function ($, window) {
    // 初始化插件参数并配置插件各项事件
    function init(options) {
        var defaults = {
            userLimit: 1, //可选择的人数
            userHasSelected: "", //当前已选择的人员信息
            callback: function () { } //点击确定按钮之后需要执行的回调函数
        };
        var opts = $.extend(defaults, options);
        
        var html = '<div class="modal fade" id="userSelModal" data-backdrop="static" tabindex="-1" role="dialog" aria-labelledby="userSelLabel" aria-hidden="true" style="z-index:2000;">\
                    <div class="modal-dialog">\
                    <div class="modal-content">\
                        <div class="modal-header">\
                            <button type="button" class="close" data-dismiss="modal" aria-label="Close"><span aria-hidden="true">&times;</span></button>\
                            <h4 class="modal-title"></h4>\
                        </div>\
                        <div class="modal-body" style="min-height:280px;max-height:400px;overflow:auto;">\
                            <div class="input-group">\
                                <input type="text" class="form-control" id="u_sel_query_str" placeholder="输入厂牌或姓名搜索">\
                                <span class="input-group-btn">\
                                    <button class="btn btn-primary" type="button" id="u_sel_query_bt">搜索</button>\
                                </span>\
                            </div>\
                            <table class="table table-condensed table-hover" id="u_sel_tb" style="margin:10px 0;">\
                                <thead>\
                                    <tr>\
                                        <th style="width:25%;">厂牌</th>\
                                        <th style="width:25%;">姓名</th>\
                                        <th style="width:40%;">部门</th>\
                                        <th style="width:10%;">选择</th>\
                                    </tr>\
                                </thead>\
                                <tbody id="u_sel_tb_users"></tbody>\
                            </table>\
                        </div>\
                        <div class="modal-footer">\
                            <div class="text-danger small text-left"><i class="fa fa-info-circle"></i> 默认会带出最近已选择的几位员工信息可供选择，如您所需要的员工不在列表中，请在上面搜索</div> \
                            <div class="text-left" id="u_sel_selected_users">\
                                已选择人员：&nbsp;&nbsp;\
                                </div>\
                            <button type="button" class="btn btn-default" data-dismiss="modal">取消</button>\
                            <button type="button" class="btn btn-success" id="u_sel_btn">确认</button>\
                        </div>\
                    </div>\
                </div>\
            </div>\
        ';
        var target = $(html);
        var queryBox = target.find("#u_sel_query_str"); //搜索人员文本框
        var queryButton = target.find("#u_sel_query_bt"); //搜索按钮
        var queryResultArea = target.find("#u_sel_tb_users"); //搜索结果显示区域
        var selectedArea = target.find("#u_sel_selected_users"); //已选择人员显示区域
        var confirmButton = target.find("#u_sel_btn"); //对话框确认按钮
        // 增加人员到已选择区域
        var addUser = function (u) {
            selectedArea.append("<a style='padding:2px;margin-right:8px;'><span>" + u + "</span> <i class='fa fa-remove' style='cursor:pointer;'></i></a>");
        };
        //当前已选择人员数组，默认先从传入人员中构造出数组
        var selectedUsers = (function () {
            var result = [];
            var usersIn = opts.userHasSelected.split(";");
            for (var i = 0; i < usersIn.length; i++) {
                if ($.trim(usersIn[i]) != "") {
                    result.push(usersIn[i]);
                    addUser(usersIn[i]);
                }                
            }
            return result;
        })();
        
        //删除已选择人员
        selectedArea.on("click", "a i", function () {
            var deletedUser = $(this).parent("a").find("span").html();
            if (selectedUsers.indexOf(deletedUser) >= 0) {
                selectedUsers.splice(selectedUsers.indexOf(deletedUser), 1);
            }
            $(this).parent("a").remove();
        });
        
        //文本框中设置回车键触发开始搜索
        queryBox.on("keyup",function () {
            if (event.keyCode == 13) {
                queryButton.trigger("click");
            }
        });

        //搜索按钮点击开始搜索
        queryButton.on("click", function () {
            var queryStr = queryBox.val();
            if (queryStr.length < 2) {
                toastr.error("至少输入2个字符再搜索");
                return;
            }
            queryResultArea.empty();
            $.post("../Item/SearchWorkingEmp", { queryString: queryStr }, function (data) {
                if (!data.suc) {
                    toastr.error(data.msg);
                    return;
                }
                var emps = data.result;
                if (emps.length == 0) {
                    toastr.error("找不到符合条件的在职员工");
                    return;
                }
                if (emps.length == 100) {
                    toastr.info("只显示符合搜索条件的前100位员工");
                }
                for (var i = 0; i < emps.length; i++) {
                    queryResultArea.append("<tr><td>" + emps[i].cardNumber + "</td><td>" + emps[i].userName + "</td><td>" + emps[i].depName + "</td><td><button type='button' class='btn btn-xs btn-default'><i class='fa fa-check'></i></button></td></tr>");
                }
            });
        });
        
        //搜索列表中点击按钮加入到已选择人员
        queryResultArea.on("click", "tr td button", function () {
            var info = $(this).parents("tr").find("td");
            var uNum = $(info[0]).html();
            var uName = $(info[1]).html();
            var u = uName + "(" + uNum + ")";
            if (selectedUsers.indexOf(u) < 0) {
                selectedUsers.push(u);
                addUser(u);
            }
        });

        //对话框确认按钮，返回并执行回调事件
        confirmButton.on("click", function () {
            if (selectedUsers.length > opts.userLimit) {
                toastr.error("所选人数不得超过" + opts.userLimit + "人");
                return;
            }
            opts.callback(selectedUsers.join(";"));

            //记录已选择的人员
            $.post("../Item/RecordSelectedUser", { selectInfos: selectedUsers.join(";") }, function (data) { });

            $(target).modal("hide");
        });

        target.find(".modal-title").html("选择员工(人数限制：" + opts.userLimit + "）");
        $(target).modal("show");

        // 对话框打开之后，输入框自动获取焦点
        $(target).on('shown.bs.modal', function (e) {
            queryBox.focus();
        });

        // 对话框关掉之后，删除自身
        $(target).on('hidden.bs.modal', function (e) {
            target.remove();
        });

        //2021-11-26 可带出最近10个已选择的人员出来，这样就不需要再次搜索
        $.post("../Item/GetSelectedUser", {}, function (data) {
            if (data.length > 0) {
                for (var i = 0; i < data.length; i++) {
                    queryResultArea.append("<tr><td>" + data[i].select_user_no + "</td><td>" + data[i].select_user_name + "</td><td>" + data[i].select_user_dept + "</td><td><button type='button' class='btn btn-xs btn-default'><i class='fa fa-check'></i></button></td></tr>");
                }
            }
        });

    }

    //对外主接口,调用方式：$.selectUsers(options);
    $.extend({
        "selectUsers": function (options) {
            init(options);
        }
    });

})(jQuery, window);