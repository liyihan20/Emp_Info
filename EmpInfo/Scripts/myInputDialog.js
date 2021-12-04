(function ($, window) {
    var target = null;
    
    var inputDialog = {
        // 初始化插件参数并配置插件各项事件
        init: function (options) {
            var defaults = {
                title: "信息录入框",
                controls: [{ text: "请输入", name: "inputName", defaultValue: "", placeholder: "请输入" }], //可有多个input框
                confirmButtonText: "确定",
                cancelButtonText: "取消",

                callback: function () { } //点击确定按钮之后需要执行的回调函数
            };
            var opts = $.extend({}, defaults, options);

            var html = '<div class="modal fade" id="myInputModal" data-backdrop="static" tabindex="-1" role="dialog" aria-labelledby="inputModalLabel" aria-hidden="true"> \
                        <div class="modal-dialog"> \
                            <div class="modal-content"> \
                                <div class="modal-header"> \
                                    <button type="button" class="close" data-dismiss="modal" aria-label="Close"><span aria-hidden="true">&times;</span></button> \
                                    <h4 class="modal-title"><i class="fa fa-question-circle"></i> <span id="my_input_title"></span></h4> \
                                </div> \
                                <div class="modal-body"> \
                                    <table style="margin:10px 0;width:100%;"> \
                                        <tbody id="my_input_content"></tbody>\
                                    </table>\
                                </div> \
                                <div class="modal-footer"> \
                                    <button type="button" class="btn btn-default" data-dismiss="modal"><i class="fa fa-mail-reply"></i> <span id="my_input_cancel_text"></span></button> \
                                    <button id="my_input_confirm_button" type="button" class="btn btn-success"><i class="fa fa-check"></i> <span id="my_input_confirm_text"></span></button> \
                                </div> \
                            </div> \
                        </div> \
                    </div> \
        ';
            target = $(html);
            var controlsBody = target.find("#my_input_content"); //input控件区域
            var confirmButton = target.find("#my_input_confirm_button"); //对话框确认按钮

            target.find("#my_input_title").html(opts.title);
            target.find("#my_input_cancel_text").html(opts.cancelButtonText);
            target.find("#my_input_confirm_text").html(opts.confirmButtonText);

            if (opts.controls && $.isArray(opts.controls)) {
                for (var i = 0; i < opts.controls.length; i++) {
                    controlsBody.append('<tr><td style="width:100px;">' + opts.controls[i].text + '：</td><td><input class="form-control" type="text" name="' + opts.controls[i].name + '" value="' + opts.controls[i].defaultValue + '" placeholder="' + opts.controls[i].placeholder + '" style="margin-bottom:6px;" /></td></tr>');
                }
            }


            //对话框确认按钮，获取输入的值并执行回调函数,返回一个对象，格式：{name1:value1,name2:value2}
            confirmButton.on("click", function () {
                var inputResult = {};
                controlsBody.find("input").each(function (idx, ele) {
                    inputResult[$(ele).attr("name")] = $(ele).val();
                });
                console.log(inputResult);
                if (opts.callback(inputResult)) {
                    $(target).modal("hide");
                }
            });

            //输入框回车键触发确定按钮
            controlsBody.find("input").keyup(function () {
                if (event.keyCode == 13) {
                    confirmButton.trigger("click");
                }
            });

            $(target).modal("show");

            // 对话框关掉之后，删除自身
            $(target).on('hidden.bs.modal', function (e) {
                target.remove();
            });
        },
        //关闭操作
        close: function () {
            if (target) {
                $(target).modal("hide");
            }
        }
    };    

    //对外主接口,调用方式：$.inputDialog(options);
    $.extend({
        "inputDialog": function (options) {
            inputDialog.init(options);
            return inputDialog;
        }
    });

})(jQuery, window);