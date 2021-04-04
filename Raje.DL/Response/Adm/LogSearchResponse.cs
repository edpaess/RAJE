﻿using Raje.DL.Response.Base;
using Raje.DL.Services.DAL.Model;

namespace Raje.DL.Response.Adm
{
    public class LogSearchResponse : BaseResponse, IEntity
    {
        public bool Input { get; set; }

        public short Code { get; set; }

        public string Api { get; set; }

        public string UrlQuery { get; set; }

        public string Method { get; set; }
    }
}
