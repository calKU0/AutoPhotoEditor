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
        public string OpeIdent => _sessionId > 0 ? _xlLogin.OpeIdent : "";

        public bool Login()
        {
            XLLoginInfo_20241 xLLoginInfo = new()
            {
                Wersja = _xlLogin.ApiVersion,
                ProgramID = _xlLogin.ProgramName,
            };

            int result = cdn_api.cdn_api.XLLogin(xLLoginInfo, ref _sessionId);
            if (result != 0)
            {
                throw new InvalidOperationException("XL login failed with result: " + result);
            }
            _xlLogin.OpeIdent = xLLoginInfo.OpeIdent;

            return true;
        }

        public bool Logout()
        {
            XLLogoutInfo_20241 xLLogoutInfo = new()
            {
                Wersja = _xlLogin.ApiVersion,
            };

            if (_sessionId > 0)
            {
                int result = cdn_api.cdn_api.XLLogout(_sessionId);
                if (result != 0)
                {
                    throw new InvalidOperationException("XL logout failed with result: " + result);
                }
                _sessionId = 0;
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