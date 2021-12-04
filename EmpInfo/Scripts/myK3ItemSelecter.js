(function ($, window) {
    // 初始化插件参数并配置插件各项事件
    function init(options) {
        var defaults = {
            company: "", //当前帐套
            itemModel: "", //当前型号
            callback: function () { } //点击确定按钮之后需要执行的回调函数
        };
        var opts = $.extend(defaults, options);
        
        var html = '<div class="modal fade" id="k3ItemSelModal" data-backdrop="static" tabindex="-1" role="dialog" aria-labelledby="k3ItemSelLabel" aria-hidden="true" style="z-index:1500;">\
                    <div class="modal-dialog">\
                    <div class="modal-content">\
                        <div class="modal-header">\
                            <button type="button" class="close" data-dismiss="modal" aria-label="Close"><span aria-hidden="true">&times;</span></button>\
                            <h4 class="modal-title"></h4>\
                        </div>\
                        <div class="modal-body" style="min-height:280px;max-height:400px;overflow:auto;">\
                            <div class="input-group">\
                                <input type="text" class="form-control" id="u_sel_query_str" placeholder="输入名称或型号搜索">\
                                <span class="input-group-btn">\
                                    <button class="btn btn-primary" type="button" id="u_sel_query_bt" data-loading-text="正在查询..">搜索</button>\
                                </span>\
                            </div>\
                            <table class="table table-condensed table-hover" id="u_sel_tb" style="margin:10px 0;">\
                                <thead>\
                                    <tr>\
                                        <th style="width:10%;text-align:center;">选择</th>\
                                        <th style="width:40%;">名称</th>\
                                        <th style="width:50%;">型号</th>\
                                    </tr>\
                                </thead>\
                                <tbody id="u_sel_tb_k3_items"></tbody>\
                            </table>\
                        </div>\
                        <div class="modal-footer">\
                            <button type="button" class="btn btn-default" data-dismiss="modal">取消</button>\
                            <button type="button" class="btn btn-success" id="u_sel_btn">确认</button>\
                        </div>\
                    </div>\
                </div>\
            </div>\
        ';
        var target = $(html);
        var queryBox = target.find("#u_sel_query_str"); //搜索文本框
        var queryButton = target.find("#u_sel_query_bt"); //搜索按钮
        var queryResultArea = target.find("#u_sel_tb_k3_items"); //搜索结果显示区域
        var confirmButton = target.find("#u_sel_btn"); //对话框确认按钮
        var tempItems = []; //搜索出来的临时结果
        
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
            $(queryButton).button("loading");
            queryResultArea.empty();
            $.post("../Item/GetK3ItemInfo", { company: opts.company, itemInfo: queryStr }, function (data) {
                $(queryButton).button("reset");
                if (data.length == 0) {
                    toastr.error("找不到符合条件的记录");
                    return;
                }
                if (data.length == 50) {
                    toastr.info("只显示符合搜索条件的前50行记录");
                }
                for (var i = 0; i < data.length; i++) {
                    queryResultArea.append("<tr><td style='text-align:center;'><input type='radio' name='item_checked' id='" + data[i].item_id + "' /></td><td>" + data[i].item_name + "</td><td>" + data[i].item_model + "</td></tr>");
                }
                tempItems = data;                
            });
        });
        
        //搜索列表中点击选中单选按钮
        queryResultArea.on("click", "tr", function () {
            var radio = $($(this).find("td")[0]).find("input")[0];
            $(radio).attr("checked", "checked");
        });

        //对话框确认按钮，返回并执行回调事件
        confirmButton.on("click", function () {
            var checked = $(queryResultArea).find("input:checked");
            if (checked.length < 1) {
                toastr.error("请先勾选型号");
                return;
            }
            var itemId = $(checked[0]).attr("id");
            var checkedItem = tempItems.filter(function (t) { return t.item_id == itemId });            
            opts.callback(checkedItem[0]);
            $(target).modal("hide");
        });

        target.find(".modal-title").html("选择K3物料/产品(公司：" + opts.company + "）");
        $(target).modal("show");

        //对话框打开之后，输入框自动获取焦点
        $(target).on('shown.bs.modal', function (e) {
            queryBox.focus();
            $(queryBox).val(opts.itemModel);
        });

        // 对话框关掉之后，删除自身
        $(target).on('hidden.bs.modal', function (e) {
            target.remove();
        });
    }

    //对外主接口,调用方式：$.selectUsers(options);
    $.extend({
        "selectK3Items": function (options) {
            init(options);
        }
    });

})(jQuery, window);