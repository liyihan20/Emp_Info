using EmpInfo.Models;

namespace EmpInfo.Interfaces
{
    interface IJsInterface
    {
        RedirectModel HandleJsInterface(string result,UserInfo userInfo);
    }
}
