(function ($, window) {

    // 初始化插件参数并配置插件各项事件
    function init(item,sysNum, options) {
        var defaults = {
            swf: '../Content/webupload/Uploader.swf',
            server: '../File/BeginUpload',
            resize: false,
            auto: true,
            pick: "#picker",
            formData: { sysNum: sysNum },
            //fileNumLimit: 3,
            fileSingleSizeLimit: 100 * 1024 * 1024, //默认限制大小100M
            accept: {
                title: 'Images',
                extensions: 'gif,jpg,jpeg,bmp,png',
                mimeTypes: 'image/*'
            }
        };
        var opts = $.extend(defaults, options);
        var prefix = opts.prefix || ""; //文件名前缀，同一表单有多个上传控件时，需要在文件名前面加上前缀以区分是哪部分上传的，并且需分开显示文件列表
        var target = $(item);

        var uploader = WebUploader.create(defaults);
        var html = '<div class="progress">\
                        <div class="progress-bar" style="width: 0%;">0%</div>\
                    </div>\
                    <ul class="list-group">\
                    </ul>';
        target.append(html);

        var pbar = target.find(".progress");
        var fileListEle = target.find(".list-group");
        $(pbar).hide(); //一开始隐藏进度条

        var addFileEle = function (fileName, fileSize, fileId) {            
            var shortName = fileName;
            if (shortName.length > 25) {
                var ext = shortName.substr(shortName.lastIndexOf("."));
                shortName = shortName.substr(0, 20) + ".." + ext;
            }
            $(fileListEle).append('<li class="list-group-item">\
                                    <a href="#" data-fd="' + fileName + '" data-id="' + fileId + '"><i class="fa fa-download"></i> ' + shortName + '(' + (fileSize > 1000000 ? ((fileSize / 1000000).toFixed(1) + 'M') : ((fileSize / 1000).toFixed(1) + 'K')) + ')</a>\
                                    <span class="text-danger" style="padding-left:16px;cursor:pointer;"><i class="fa fa-close" title="删除"></i> </span>\
                                    </li>');
        };

        //获取已上传的文件
        $.post("../Item/GetAttachments", { sysNo: sysNum }, function (data) {
            if (data.length > 0) {
                for (var i = 0; i < data.length; i++) {
                    if (data[i].fileName.indexOf(prefix) == 0) { //符合前缀的才显示出来
                        addFileEle(data[i].fileName, data[i].fileSize);
                    }
                }
            }
        });

        uploader.on('fileQueued', function (file) {
            console.log(file);
            file.name = prefix + file.name; //先加上前缀
            file.name = file.name.replace(/&/g, "_").replace(/ /g, "").replace(/#/g, "_"); //将文件名中的&转化为_，空格去掉,将#转化为_
        });

        uploader.on('uploadProgress', function (file, percentage) {
            $(pbar).show();
            $(pbar).find(".progress-bar").css("width", percentage * 100 + "%").html(percentage * 100 + "%");
        });

        uploader.on('uploadSuccess', function (file, res) {
            if (!res.suc) {
                console.log(file.name + ":上传出错，原因：" + res);
                return;
            }
            addFileEle(file.name, file.size,file.id);
        });

        uploader.on('uploadComplete', function (file) {
            $(pbar).fadeOut();
        });

        uploader.on('uploadError', function (file) {
            toastr.error(file.name + ":上传失败");
            uploader.removeFile(file.id, true);
        });

        uploader.on("error", function (type) {
            switch (type) {
                case "Q_TYPE_DENIED":
                    toastr.error("文件格式不正确");
                    break;
                case "F_EXCEED_SIZE":
                    toastr.error("单个文件大小必须少于" + (defaults.fileSingleSizeLimit / 1024 / 1024) + "M");
                    break;
                case "F_DUPLICATE":
                    toastr.error("不能重复上传文件");
                    break;
                case "Q_EXCEED_NUM_LIMIT":
                    toastr.error("最多上传文件数量是" + defaults.fileNumLimit + "个");
                    break;
                default:
                    console.error("上传失败：" + type);
                    break;
            }
        });

        //下载
        $(fileListEle).on("click", "a", function () {
            var fileName = $(this).attr("data-fd");
            var url = utils.GetDownloadRoute(sysNum) + fileName;
            window.open(url);
        });

        //删除
        $(fileListEle).on("click", ".fa-close", function () {
            var file = $(this).parents("li").find("a")[0];
            var fileName = $(file).attr("data-fd");
            var fileId = $(file).attr("data-id");
            openConfirmDialog("确定要删除此文件吗？", function () {
                $.post("../File/RemoveUploadedFile", { sysNum: sysNum, fileName: fileName }, function (data) {
                    if (data.suc) {
                        toastr.success("文件移除成功");
                        $(file).parents("li").remove();
                        uploader.removeFile(fileId, true);
                    } else {
                        toastr.error("移除失败：" + data.msg);
                    }
                });
            });
        });
    }

    //对外主接口
    $.fn.myUploader = function (options) {
        var ele = this;
        init(ele, options.sysNum, options);
    }

    //上传插件数量
    $.fn.getMyUploaderFilesNum = function (options) {
        var ele = this;
        return $(ele).find(".list-group li").length || "0";
    }

})(jQuery, window);