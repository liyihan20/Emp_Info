﻿//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.18444
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

namespace EmpInfo.QywxWebSrv {
    
    
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.ServiceModel", "4.0.0.0")]
    [System.ServiceModel.ServiceContractAttribute(Namespace="http://ic.truly.com.cn/", ConfigurationName="QywxWebSrv.QywxApiSrvSoap")]
    public interface QywxApiSrvSoap {
        
        // CODEGEN: 命名空间 http://ic.truly.com.cn/ 的元素名称 secret 以后生成的消息协定未标记为 nillable
        [System.ServiceModel.OperationContractAttribute(Action="http://ic.truly.com.cn/GetAccessToken", ReplyAction="*")]
        EmpInfo.QywxWebSrv.GetAccessTokenResponse GetAccessToken(EmpInfo.QywxWebSrv.GetAccessTokenRequest request);
        
        // CODEGEN: 命名空间 http://ic.truly.com.cn/ 的元素名称 openId 以后生成的消息协定未标记为 nillable
        [System.ServiceModel.OperationContractAttribute(Action="http://ic.truly.com.cn/GetUserIdByOpenId", ReplyAction="*")]
        EmpInfo.QywxWebSrv.GetUserIdByOpenIdResponse GetUserIdByOpenId(EmpInfo.QywxWebSrv.GetUserIdByOpenIdRequest request);
        
        // CODEGEN: 命名空间 http://ic.truly.com.cn/ 的元素名称 userId 以后生成的消息协定未标记为 nillable
        [System.ServiceModel.OperationContractAttribute(Action="http://ic.truly.com.cn/GetOpenIdByUserId", ReplyAction="*")]
        EmpInfo.QywxWebSrv.GetOpenIdByUserIdResponse GetOpenIdByUserId(EmpInfo.QywxWebSrv.GetOpenIdByUserIdRequest request);
        
        // CODEGEN: 命名空间 http://ic.truly.com.cn/ 的元素名称 redirectUrl 以后生成的消息协定未标记为 nillable
        [System.ServiceModel.OperationContractAttribute(Action="http://ic.truly.com.cn/GetOAthLink", ReplyAction="*")]
        EmpInfo.QywxWebSrv.GetOAthLinkResponse GetOAthLink(EmpInfo.QywxWebSrv.GetOAthLinkRequest request);
        
        // CODEGEN: 命名空间 http://ic.truly.com.cn/ 的元素名称 agentId 以后生成的消息协定未标记为 nillable
        [System.ServiceModel.OperationContractAttribute(Action="http://ic.truly.com.cn/GetWebLink", ReplyAction="*")]
        EmpInfo.QywxWebSrv.GetWebLinkResponse GetWebLink(EmpInfo.QywxWebSrv.GetWebLinkRequest request);
        
        // CODEGEN: 命名空间 http://ic.truly.com.cn/ 的元素名称 secret 以后生成的消息协定未标记为 nillable
        [System.ServiceModel.OperationContractAttribute(Action="http://ic.truly.com.cn/GetUserIdFromCode", ReplyAction="*")]
        EmpInfo.QywxWebSrv.GetUserIdFromCodeResponse GetUserIdFromCode(EmpInfo.QywxWebSrv.GetUserIdFromCodeRequest request);
    }
    
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.ServiceModel", "4.0.0.0")]
    [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Advanced)]
    [System.ServiceModel.MessageContractAttribute(IsWrapped=false)]
    public partial class GetAccessTokenRequest {
        
        [System.ServiceModel.MessageBodyMemberAttribute(Name="GetAccessToken", Namespace="http://ic.truly.com.cn/", Order=0)]
        public EmpInfo.QywxWebSrv.GetAccessTokenRequestBody Body;
        
        public GetAccessTokenRequest() {
        }
        
        public GetAccessTokenRequest(EmpInfo.QywxWebSrv.GetAccessTokenRequestBody Body) {
            this.Body = Body;
        }
    }
    
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.ServiceModel", "4.0.0.0")]
    [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Advanced)]
    [System.Runtime.Serialization.DataContractAttribute(Namespace="http://ic.truly.com.cn/")]
    public partial class GetAccessTokenRequestBody {
        
        [System.Runtime.Serialization.DataMemberAttribute(EmitDefaultValue=false, Order=0)]
        public string secret;
        
        public GetAccessTokenRequestBody() {
        }
        
        public GetAccessTokenRequestBody(string secret) {
            this.secret = secret;
        }
    }
    
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.ServiceModel", "4.0.0.0")]
    [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Advanced)]
    [System.ServiceModel.MessageContractAttribute(IsWrapped=false)]
    public partial class GetAccessTokenResponse {
        
        [System.ServiceModel.MessageBodyMemberAttribute(Name="GetAccessTokenResponse", Namespace="http://ic.truly.com.cn/", Order=0)]
        public EmpInfo.QywxWebSrv.GetAccessTokenResponseBody Body;
        
        public GetAccessTokenResponse() {
        }
        
        public GetAccessTokenResponse(EmpInfo.QywxWebSrv.GetAccessTokenResponseBody Body) {
            this.Body = Body;
        }
    }
    
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.ServiceModel", "4.0.0.0")]
    [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Advanced)]
    [System.Runtime.Serialization.DataContractAttribute(Namespace="http://ic.truly.com.cn/")]
    public partial class GetAccessTokenResponseBody {
        
        [System.Runtime.Serialization.DataMemberAttribute(EmitDefaultValue=false, Order=0)]
        public string GetAccessTokenResult;
        
        public GetAccessTokenResponseBody() {
        }
        
        public GetAccessTokenResponseBody(string GetAccessTokenResult) {
            this.GetAccessTokenResult = GetAccessTokenResult;
        }
    }
    
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.ServiceModel", "4.0.0.0")]
    [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Advanced)]
    [System.ServiceModel.MessageContractAttribute(IsWrapped=false)]
    public partial class GetUserIdByOpenIdRequest {
        
        [System.ServiceModel.MessageBodyMemberAttribute(Name="GetUserIdByOpenId", Namespace="http://ic.truly.com.cn/", Order=0)]
        public EmpInfo.QywxWebSrv.GetUserIdByOpenIdRequestBody Body;
        
        public GetUserIdByOpenIdRequest() {
        }
        
        public GetUserIdByOpenIdRequest(EmpInfo.QywxWebSrv.GetUserIdByOpenIdRequestBody Body) {
            this.Body = Body;
        }
    }
    
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.ServiceModel", "4.0.0.0")]
    [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Advanced)]
    [System.Runtime.Serialization.DataContractAttribute(Namespace="http://ic.truly.com.cn/")]
    public partial class GetUserIdByOpenIdRequestBody {
        
        [System.Runtime.Serialization.DataMemberAttribute(EmitDefaultValue=false, Order=0)]
        public string openId;
        
        public GetUserIdByOpenIdRequestBody() {
        }
        
        public GetUserIdByOpenIdRequestBody(string openId) {
            this.openId = openId;
        }
    }
    
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.ServiceModel", "4.0.0.0")]
    [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Advanced)]
    [System.ServiceModel.MessageContractAttribute(IsWrapped=false)]
    public partial class GetUserIdByOpenIdResponse {
        
        [System.ServiceModel.MessageBodyMemberAttribute(Name="GetUserIdByOpenIdResponse", Namespace="http://ic.truly.com.cn/", Order=0)]
        public EmpInfo.QywxWebSrv.GetUserIdByOpenIdResponseBody Body;
        
        public GetUserIdByOpenIdResponse() {
        }
        
        public GetUserIdByOpenIdResponse(EmpInfo.QywxWebSrv.GetUserIdByOpenIdResponseBody Body) {
            this.Body = Body;
        }
    }
    
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.ServiceModel", "4.0.0.0")]
    [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Advanced)]
    [System.Runtime.Serialization.DataContractAttribute(Namespace="http://ic.truly.com.cn/")]
    public partial class GetUserIdByOpenIdResponseBody {
        
        [System.Runtime.Serialization.DataMemberAttribute(EmitDefaultValue=false, Order=0)]
        public string GetUserIdByOpenIdResult;
        
        public GetUserIdByOpenIdResponseBody() {
        }
        
        public GetUserIdByOpenIdResponseBody(string GetUserIdByOpenIdResult) {
            this.GetUserIdByOpenIdResult = GetUserIdByOpenIdResult;
        }
    }
    
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.ServiceModel", "4.0.0.0")]
    [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Advanced)]
    [System.ServiceModel.MessageContractAttribute(IsWrapped=false)]
    public partial class GetOpenIdByUserIdRequest {
        
        [System.ServiceModel.MessageBodyMemberAttribute(Name="GetOpenIdByUserId", Namespace="http://ic.truly.com.cn/", Order=0)]
        public EmpInfo.QywxWebSrv.GetOpenIdByUserIdRequestBody Body;
        
        public GetOpenIdByUserIdRequest() {
        }
        
        public GetOpenIdByUserIdRequest(EmpInfo.QywxWebSrv.GetOpenIdByUserIdRequestBody Body) {
            this.Body = Body;
        }
    }
    
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.ServiceModel", "4.0.0.0")]
    [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Advanced)]
    [System.Runtime.Serialization.DataContractAttribute(Namespace="http://ic.truly.com.cn/")]
    public partial class GetOpenIdByUserIdRequestBody {
        
        [System.Runtime.Serialization.DataMemberAttribute(EmitDefaultValue=false, Order=0)]
        public string userId;
        
        public GetOpenIdByUserIdRequestBody() {
        }
        
        public GetOpenIdByUserIdRequestBody(string userId) {
            this.userId = userId;
        }
    }
    
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.ServiceModel", "4.0.0.0")]
    [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Advanced)]
    [System.ServiceModel.MessageContractAttribute(IsWrapped=false)]
    public partial class GetOpenIdByUserIdResponse {
        
        [System.ServiceModel.MessageBodyMemberAttribute(Name="GetOpenIdByUserIdResponse", Namespace="http://ic.truly.com.cn/", Order=0)]
        public EmpInfo.QywxWebSrv.GetOpenIdByUserIdResponseBody Body;
        
        public GetOpenIdByUserIdResponse() {
        }
        
        public GetOpenIdByUserIdResponse(EmpInfo.QywxWebSrv.GetOpenIdByUserIdResponseBody Body) {
            this.Body = Body;
        }
    }
    
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.ServiceModel", "4.0.0.0")]
    [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Advanced)]
    [System.Runtime.Serialization.DataContractAttribute(Namespace="http://ic.truly.com.cn/")]
    public partial class GetOpenIdByUserIdResponseBody {
        
        [System.Runtime.Serialization.DataMemberAttribute(EmitDefaultValue=false, Order=0)]
        public string GetOpenIdByUserIdResult;
        
        public GetOpenIdByUserIdResponseBody() {
        }
        
        public GetOpenIdByUserIdResponseBody(string GetOpenIdByUserIdResult) {
            this.GetOpenIdByUserIdResult = GetOpenIdByUserIdResult;
        }
    }
    
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.ServiceModel", "4.0.0.0")]
    [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Advanced)]
    [System.ServiceModel.MessageContractAttribute(IsWrapped=false)]
    public partial class GetOAthLinkRequest {
        
        [System.ServiceModel.MessageBodyMemberAttribute(Name="GetOAthLink", Namespace="http://ic.truly.com.cn/", Order=0)]
        public EmpInfo.QywxWebSrv.GetOAthLinkRequestBody Body;
        
        public GetOAthLinkRequest() {
        }
        
        public GetOAthLinkRequest(EmpInfo.QywxWebSrv.GetOAthLinkRequestBody Body) {
            this.Body = Body;
        }
    }
    
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.ServiceModel", "4.0.0.0")]
    [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Advanced)]
    [System.Runtime.Serialization.DataContractAttribute(Namespace="http://ic.truly.com.cn/")]
    public partial class GetOAthLinkRequestBody {
        
        [System.Runtime.Serialization.DataMemberAttribute(EmitDefaultValue=false, Order=0)]
        public string redirectUrl;
        
        public GetOAthLinkRequestBody() {
        }
        
        public GetOAthLinkRequestBody(string redirectUrl) {
            this.redirectUrl = redirectUrl;
        }
    }
    
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.ServiceModel", "4.0.0.0")]
    [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Advanced)]
    [System.ServiceModel.MessageContractAttribute(IsWrapped=false)]
    public partial class GetOAthLinkResponse {
        
        [System.ServiceModel.MessageBodyMemberAttribute(Name="GetOAthLinkResponse", Namespace="http://ic.truly.com.cn/", Order=0)]
        public EmpInfo.QywxWebSrv.GetOAthLinkResponseBody Body;
        
        public GetOAthLinkResponse() {
        }
        
        public GetOAthLinkResponse(EmpInfo.QywxWebSrv.GetOAthLinkResponseBody Body) {
            this.Body = Body;
        }
    }
    
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.ServiceModel", "4.0.0.0")]
    [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Advanced)]
    [System.Runtime.Serialization.DataContractAttribute(Namespace="http://ic.truly.com.cn/")]
    public partial class GetOAthLinkResponseBody {
        
        [System.Runtime.Serialization.DataMemberAttribute(EmitDefaultValue=false, Order=0)]
        public string GetOAthLinkResult;
        
        public GetOAthLinkResponseBody() {
        }
        
        public GetOAthLinkResponseBody(string GetOAthLinkResult) {
            this.GetOAthLinkResult = GetOAthLinkResult;
        }
    }
    
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.ServiceModel", "4.0.0.0")]
    [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Advanced)]
    [System.ServiceModel.MessageContractAttribute(IsWrapped=false)]
    public partial class GetWebLinkRequest {
        
        [System.ServiceModel.MessageBodyMemberAttribute(Name="GetWebLink", Namespace="http://ic.truly.com.cn/", Order=0)]
        public EmpInfo.QywxWebSrv.GetWebLinkRequestBody Body;
        
        public GetWebLinkRequest() {
        }
        
        public GetWebLinkRequest(EmpInfo.QywxWebSrv.GetWebLinkRequestBody Body) {
            this.Body = Body;
        }
    }
    
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.ServiceModel", "4.0.0.0")]
    [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Advanced)]
    [System.Runtime.Serialization.DataContractAttribute(Namespace="http://ic.truly.com.cn/")]
    public partial class GetWebLinkRequestBody {
        
        [System.Runtime.Serialization.DataMemberAttribute(EmitDefaultValue=false, Order=0)]
        public string agentId;
        
        [System.Runtime.Serialization.DataMemberAttribute(EmitDefaultValue=false, Order=1)]
        public string redirectUrl;
        
        [System.Runtime.Serialization.DataMemberAttribute(EmitDefaultValue=false, Order=2)]
        public string state;
        
        public GetWebLinkRequestBody() {
        }
        
        public GetWebLinkRequestBody(string agentId, string redirectUrl, string state) {
            this.agentId = agentId;
            this.redirectUrl = redirectUrl;
            this.state = state;
        }
    }
    
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.ServiceModel", "4.0.0.0")]
    [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Advanced)]
    [System.ServiceModel.MessageContractAttribute(IsWrapped=false)]
    public partial class GetWebLinkResponse {
        
        [System.ServiceModel.MessageBodyMemberAttribute(Name="GetWebLinkResponse", Namespace="http://ic.truly.com.cn/", Order=0)]
        public EmpInfo.QywxWebSrv.GetWebLinkResponseBody Body;
        
        public GetWebLinkResponse() {
        }
        
        public GetWebLinkResponse(EmpInfo.QywxWebSrv.GetWebLinkResponseBody Body) {
            this.Body = Body;
        }
    }
    
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.ServiceModel", "4.0.0.0")]
    [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Advanced)]
    [System.Runtime.Serialization.DataContractAttribute(Namespace="http://ic.truly.com.cn/")]
    public partial class GetWebLinkResponseBody {
        
        [System.Runtime.Serialization.DataMemberAttribute(EmitDefaultValue=false, Order=0)]
        public string GetWebLinkResult;
        
        public GetWebLinkResponseBody() {
        }
        
        public GetWebLinkResponseBody(string GetWebLinkResult) {
            this.GetWebLinkResult = GetWebLinkResult;
        }
    }
    
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.ServiceModel", "4.0.0.0")]
    [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Advanced)]
    [System.ServiceModel.MessageContractAttribute(IsWrapped=false)]
    public partial class GetUserIdFromCodeRequest {
        
        [System.ServiceModel.MessageBodyMemberAttribute(Name="GetUserIdFromCode", Namespace="http://ic.truly.com.cn/", Order=0)]
        public EmpInfo.QywxWebSrv.GetUserIdFromCodeRequestBody Body;
        
        public GetUserIdFromCodeRequest() {
        }
        
        public GetUserIdFromCodeRequest(EmpInfo.QywxWebSrv.GetUserIdFromCodeRequestBody Body) {
            this.Body = Body;
        }
    }
    
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.ServiceModel", "4.0.0.0")]
    [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Advanced)]
    [System.Runtime.Serialization.DataContractAttribute(Namespace="http://ic.truly.com.cn/")]
    public partial class GetUserIdFromCodeRequestBody {
        
        [System.Runtime.Serialization.DataMemberAttribute(EmitDefaultValue=false, Order=0)]
        public string secret;
        
        [System.Runtime.Serialization.DataMemberAttribute(EmitDefaultValue=false, Order=1)]
        public string code;
        
        public GetUserIdFromCodeRequestBody() {
        }
        
        public GetUserIdFromCodeRequestBody(string secret, string code) {
            this.secret = secret;
            this.code = code;
        }
    }
    
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.ServiceModel", "4.0.0.0")]
    [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Advanced)]
    [System.ServiceModel.MessageContractAttribute(IsWrapped=false)]
    public partial class GetUserIdFromCodeResponse {
        
        [System.ServiceModel.MessageBodyMemberAttribute(Name="GetUserIdFromCodeResponse", Namespace="http://ic.truly.com.cn/", Order=0)]
        public EmpInfo.QywxWebSrv.GetUserIdFromCodeResponseBody Body;
        
        public GetUserIdFromCodeResponse() {
        }
        
        public GetUserIdFromCodeResponse(EmpInfo.QywxWebSrv.GetUserIdFromCodeResponseBody Body) {
            this.Body = Body;
        }
    }
    
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.ServiceModel", "4.0.0.0")]
    [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Advanced)]
    [System.Runtime.Serialization.DataContractAttribute(Namespace="http://ic.truly.com.cn/")]
    public partial class GetUserIdFromCodeResponseBody {
        
        [System.Runtime.Serialization.DataMemberAttribute(EmitDefaultValue=false, Order=0)]
        public string GetUserIdFromCodeResult;
        
        public GetUserIdFromCodeResponseBody() {
        }
        
        public GetUserIdFromCodeResponseBody(string GetUserIdFromCodeResult) {
            this.GetUserIdFromCodeResult = GetUserIdFromCodeResult;
        }
    }
    
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.ServiceModel", "4.0.0.0")]
    public interface QywxApiSrvSoapChannel : EmpInfo.QywxWebSrv.QywxApiSrvSoap, System.ServiceModel.IClientChannel {
    }
    
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.ServiceModel", "4.0.0.0")]
    public partial class QywxApiSrvSoapClient : System.ServiceModel.ClientBase<EmpInfo.QywxWebSrv.QywxApiSrvSoap>, EmpInfo.QywxWebSrv.QywxApiSrvSoap {
        
        public QywxApiSrvSoapClient() {
        }
        
        public QywxApiSrvSoapClient(string endpointConfigurationName) : 
                base(endpointConfigurationName) {
        }
        
        public QywxApiSrvSoapClient(string endpointConfigurationName, string remoteAddress) : 
                base(endpointConfigurationName, remoteAddress) {
        }
        
        public QywxApiSrvSoapClient(string endpointConfigurationName, System.ServiceModel.EndpointAddress remoteAddress) : 
                base(endpointConfigurationName, remoteAddress) {
        }
        
        public QywxApiSrvSoapClient(System.ServiceModel.Channels.Binding binding, System.ServiceModel.EndpointAddress remoteAddress) : 
                base(binding, remoteAddress) {
        }
        
        [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Advanced)]
        EmpInfo.QywxWebSrv.GetAccessTokenResponse EmpInfo.QywxWebSrv.QywxApiSrvSoap.GetAccessToken(EmpInfo.QywxWebSrv.GetAccessTokenRequest request) {
            return base.Channel.GetAccessToken(request);
        }
        
        public string GetAccessToken(string secret) {
            EmpInfo.QywxWebSrv.GetAccessTokenRequest inValue = new EmpInfo.QywxWebSrv.GetAccessTokenRequest();
            inValue.Body = new EmpInfo.QywxWebSrv.GetAccessTokenRequestBody();
            inValue.Body.secret = secret;
            EmpInfo.QywxWebSrv.GetAccessTokenResponse retVal = ((EmpInfo.QywxWebSrv.QywxApiSrvSoap)(this)).GetAccessToken(inValue);
            return retVal.Body.GetAccessTokenResult;
        }
        
        [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Advanced)]
        EmpInfo.QywxWebSrv.GetUserIdByOpenIdResponse EmpInfo.QywxWebSrv.QywxApiSrvSoap.GetUserIdByOpenId(EmpInfo.QywxWebSrv.GetUserIdByOpenIdRequest request) {
            return base.Channel.GetUserIdByOpenId(request);
        }
        
        public string GetUserIdByOpenId(string openId) {
            EmpInfo.QywxWebSrv.GetUserIdByOpenIdRequest inValue = new EmpInfo.QywxWebSrv.GetUserIdByOpenIdRequest();
            inValue.Body = new EmpInfo.QywxWebSrv.GetUserIdByOpenIdRequestBody();
            inValue.Body.openId = openId;
            EmpInfo.QywxWebSrv.GetUserIdByOpenIdResponse retVal = ((EmpInfo.QywxWebSrv.QywxApiSrvSoap)(this)).GetUserIdByOpenId(inValue);
            return retVal.Body.GetUserIdByOpenIdResult;
        }
        
        [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Advanced)]
        EmpInfo.QywxWebSrv.GetOpenIdByUserIdResponse EmpInfo.QywxWebSrv.QywxApiSrvSoap.GetOpenIdByUserId(EmpInfo.QywxWebSrv.GetOpenIdByUserIdRequest request) {
            return base.Channel.GetOpenIdByUserId(request);
        }
        
        public string GetOpenIdByUserId(string userId) {
            EmpInfo.QywxWebSrv.GetOpenIdByUserIdRequest inValue = new EmpInfo.QywxWebSrv.GetOpenIdByUserIdRequest();
            inValue.Body = new EmpInfo.QywxWebSrv.GetOpenIdByUserIdRequestBody();
            inValue.Body.userId = userId;
            EmpInfo.QywxWebSrv.GetOpenIdByUserIdResponse retVal = ((EmpInfo.QywxWebSrv.QywxApiSrvSoap)(this)).GetOpenIdByUserId(inValue);
            return retVal.Body.GetOpenIdByUserIdResult;
        }
        
        [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Advanced)]
        EmpInfo.QywxWebSrv.GetOAthLinkResponse EmpInfo.QywxWebSrv.QywxApiSrvSoap.GetOAthLink(EmpInfo.QywxWebSrv.GetOAthLinkRequest request) {
            return base.Channel.GetOAthLink(request);
        }
        
        public string GetOAthLink(string redirectUrl) {
            EmpInfo.QywxWebSrv.GetOAthLinkRequest inValue = new EmpInfo.QywxWebSrv.GetOAthLinkRequest();
            inValue.Body = new EmpInfo.QywxWebSrv.GetOAthLinkRequestBody();
            inValue.Body.redirectUrl = redirectUrl;
            EmpInfo.QywxWebSrv.GetOAthLinkResponse retVal = ((EmpInfo.QywxWebSrv.QywxApiSrvSoap)(this)).GetOAthLink(inValue);
            return retVal.Body.GetOAthLinkResult;
        }
        
        [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Advanced)]
        EmpInfo.QywxWebSrv.GetWebLinkResponse EmpInfo.QywxWebSrv.QywxApiSrvSoap.GetWebLink(EmpInfo.QywxWebSrv.GetWebLinkRequest request) {
            return base.Channel.GetWebLink(request);
        }
        
        public string GetWebLink(string agentId, string redirectUrl, string state) {
            EmpInfo.QywxWebSrv.GetWebLinkRequest inValue = new EmpInfo.QywxWebSrv.GetWebLinkRequest();
            inValue.Body = new EmpInfo.QywxWebSrv.GetWebLinkRequestBody();
            inValue.Body.agentId = agentId;
            inValue.Body.redirectUrl = redirectUrl;
            inValue.Body.state = state;
            EmpInfo.QywxWebSrv.GetWebLinkResponse retVal = ((EmpInfo.QywxWebSrv.QywxApiSrvSoap)(this)).GetWebLink(inValue);
            return retVal.Body.GetWebLinkResult;
        }
        
        [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Advanced)]
        EmpInfo.QywxWebSrv.GetUserIdFromCodeResponse EmpInfo.QywxWebSrv.QywxApiSrvSoap.GetUserIdFromCode(EmpInfo.QywxWebSrv.GetUserIdFromCodeRequest request) {
            return base.Channel.GetUserIdFromCode(request);
        }
        
        public string GetUserIdFromCode(string secret, string code) {
            EmpInfo.QywxWebSrv.GetUserIdFromCodeRequest inValue = new EmpInfo.QywxWebSrv.GetUserIdFromCodeRequest();
            inValue.Body = new EmpInfo.QywxWebSrv.GetUserIdFromCodeRequestBody();
            inValue.Body.secret = secret;
            inValue.Body.code = code;
            EmpInfo.QywxWebSrv.GetUserIdFromCodeResponse retVal = ((EmpInfo.QywxWebSrv.QywxApiSrvSoap)(this)).GetUserIdFromCode(inValue);
            return retVal.Body.GetUserIdFromCodeResult;
        }
    }
}
