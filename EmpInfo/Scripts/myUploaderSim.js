(function ($, window) {
    // 简化版的上传插件，只能上传一个文件，不需要进度条和表格
    // 初始化插件参数并配置插件各项事件
    function init(item, options) {

        var defaults = {
            swf: '../Content/webupload/Uploader.swf',
            server: '../File/BeginUpload',
            resize: false,
            compress: false, //默认不压缩
            auto: true,
            pick: "#picker",
            formData: {},
            fileSingleSizeLimit: 10 * 1024 * 1024, //默认限制大小10M
            accept: {
                title: 'Images',
                extensions: 'gif,jpg,jpeg,bmp,png',
                mimeTypes: 'image/*'
            }
        };
        var opts = $.extend(defaults, options);

        var uploader = WebUploader.create(defaults);

        uploader.on('beforeFileQueued', function (file) {
            if (options.beforeQueued) {
                return options.beforeQueued(uploader);                
            } else {
                return true;
            }
        });

        uploader.on('fileQueued', function (file) {
            console.log(file);
            file.name = file.name.replace(/&/g, "_").replace(/ /g, "").replace(/#/g, "_"); //将文件名中的&转化为_，空格去掉
        });

        uploader.on('uploadSuccess', function (file, res) {
            if (!res.suc) {
                console.log(file.name + ":上传出错，原因：" + res);
                return;
            }
            if (options.successCallback) {
                //有成功回调的话，执行
                options.successCallback();
            }
        });

        //uploader.on('uploadError', function (file) {
        //    toastr.error(file.name + ":上传失败");
        //});

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
        
    }

    //对外主接口
    $.fn.myUploader = function (options) {
        var ele = this;
        init(ele, options);
    }

})(jQuery, window);