﻿using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.ComponentModel.DataAnnotations;

namespace Raje.ViewModel
{
    public class SerieViewModel
    {
        public Guid Id { get; set; }

        public string Titulo { get; set; }

        public string Diretor { get; set; }

        public string Elenco { get; set; }

        public string Pais { get; set; }

        public int Ano { get; set; }

        public int NumeroTemporadas { get; set; }

        public string ImagemURL { get; set; }

        public IFormFile ImagemUpload { get; set; }

        public String Sinopse { get; set; }

        public bool Ativo { get; set; }

        [TempData]
        public string StatusMessage { get; set; }
    }
}