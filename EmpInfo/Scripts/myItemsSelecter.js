(function ($, window) {
    // 从列表中选择一项返回
    function init(options) {
        var defaults = {            
            items: [], //列表信息
            titles:[], //表格标题
            callback: function () { } //点击确定按钮之后需要执行的回调函数
        };
        var opts = $.extend(defaults, options);
        
        var html = '<div class="modal fade" id="ItemListSelModal" data-backdrop="static" tabindex="-1" role="dialog" aria-labelledby="ItemListSelLabel" aria-hidden="true" style="z-index:1500;">\
                    <div class="modal-dialog">\
                    <div class="modal-content">\
                        <div class="modal-header">\
                            <button type="button" class="close" data-dismiss="modal" aria-label="Close"><span aria-hidden="true">&times;</span></button>\
                            <h4 class="modal-title"></h4>\
                        </div>\
                        <div class="modal-body" style="min-height:200px;max-height:400px;overflow:auto;">\
                                <table data-toggle="table" id="u_item_list_tb"></table>\
                        </div>\
                        <div class="modal-footer">\
                            <button type="button" class="btn btn-default" data-dismiss="modal">取消</button>\
                            <button type="button" class="btn btn-success" id="u_confrim_btn">确认</button>\
                        </div>\
                    </div>\
                </div>\
            </div>\
        ';
        var target = $(html);
        var tb = target.find("#u_item_list_tb"); //表格
        var confirmButton = target.find("#u_confrim_btn"); //对话框确认按钮
        var columns = [];
        var rows = opts.items;
        var titles = opts.titles;
        
        columns.push({ radio: true });//单选
        if (titles.length > 0) {
            //有表格标题的
            for (var i in titles) {
                columns.push(titles[i]);
            }
        } else {
            //没有的
            for (var i in rows[0]) {
                columns.push({ field: i, title: i });
            }
        }

        $(tb).bootstrapTable({
            //height:300,
            striped: true,
            pagination: true,
            pageSize: 20,
            pageList: [20, 50, 100],
            search: false,
            clickToSelect: true,
            columns: columns,
            data: rows
        });
        

        //对话框确认按钮，返回并执行回调事件
        confirmButton.on("click", function () {
            var checkedItem = $(tb).bootstrapTable('getSelections');
            if (checkedItem.length == 0) {
                toastr.error("请先选择一行");
                return;
            }
            opts.callback(checkedItem[0]);
            $(target).modal("hide");
        });

        target.find(".modal-title").html("请从以下列表中选择1项");
        $(target).modal("show");
        

        // 对话框关掉之后，删除自身
        $(target).on('hidden.bs.modal', function (e) {
            target.remove();
        });
    }

    //对外主接口,调用方式：$.selectItemList(options);
    $.extend({
        "selectItemList": function (options) {
            init(options);
        }
    });

})(jQuery, window);