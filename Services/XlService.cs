using AutoPhotoEditor.Interfaces;
using AutoPhotoEditor.Models;
using cdn_api;

namespace AutoPhotoEditor.Services
{
    public class XlService : IXlService
    {
        private readonly XlLogin _xlLogin;
        private int _sessionId;

        public XlService(XlLogin xlLogin)
        {
            _xlLogin = xlLogin;
        }

        public bool IsLogged => _sessionId > 0;

        public bool Login()
        {
            XLLoginInfo_20241 xLLoginInfo = new()
            {
                Wersja = _xlLogin.ApiVersion,
                ProgramID = _xlLogin.ProgramName,
                Baza = _xlLogin.Database,
                OpeIdent = _xlLogin.Username,
                OpeHaslo = _xlLogin.Password,
                TrybWsadowy = _xlLogin.WithoutInterface
            };

            int result = cdn_api.cdn_api.XLLogin(xLLoginInfo, ref _sessionId);
            if (result != 0)
            {
                throw new InvalidOperationException("XL login failed with result: " + result);
            }

            return true;
        }

        public bool Logout()
        {
            XLLogoutInfo_20241 xLLogoutInfo = new()
            {
                Wersja = _xlLogin.ApiVersion,
            };

            int result = cdn_api.cdn_api.XLLogout(_sessionId);
            if (result != 0)
            {
                throw new InvalidOperationException("XL logout failed with result: " + result);
            }

            return true;
        }

        public int OpenProductList(int productId = -1)
        {
            XLGIDGrupaInfo_20241 xLGIDGrupaInfo = new()
            {
                Wersja = _xlLogin.ApiVersion,
                GIDTyp = 16,
                GIDNumer = productId
            };

            int result = cdn_api.cdn_api.XLUruchomFormatkeWgGID(xLGIDGrupaInfo);
            if (result != 0)
            {
                throw new InvalidOperationException("Failed to open product list. Result: " + result);
            }

            return xLGIDGrupaInfo.GIDNumer;
        }
    }
}