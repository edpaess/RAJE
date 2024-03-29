﻿using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Raje.Data;
using Raje.Models;
using Raje.ViewModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.WebUtilities;
using Raje.Models.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace Raje.Controllers
{
    public class ApplicationUserController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly IEmailSender _emailSender;

        public ApplicationUserController
        (
            ApplicationDbContext db,
            UserManager<IdentityUser> userManager,
            SignInManager<IdentityUser> signInManager,
            IEmailSender emailSender
        )
        {
            _db = db;
            _userManager = userManager;
            _signInManager = signInManager;
            _emailSender = emailSender;
        }

        public string ReturnUrl { get; set; }

        public IList<AuthenticationScheme> ExternalLogins { get; set; }

        [TempData]
        public string StatusMessage { get; set; }

        public IActionResult Index(string nome)
        {
            IEnumerable<ApplicationUser> users = _db.ApplicationUser.ToList();

            if (nome != null)
                users = users.Where(serie => serie.FullName.ToLower().Contains(nome.ToLower()));

            return View(users);
        }

        //GET - UPSERT
        public IActionResult Upsert(string id)
        {
            if (id == null)
            {
                ApplicationUserViewModel userNovo = new();
                //this is for create
                return View(userNovo);
            }
            else
            {
                var user = _db.ApplicationUser.Find(id);

                if (user == null)
                {
                    return NotFound();
                }

                ApplicationUserViewModel userNovo = new()
                {
                    Id = user.Id,
                    Email = user.Email,
                    FullName = user.FullName,
                    PhoneNumber = user.PhoneNumber,
                    Birthdate = user.Birthdate,
                    City = user.City,
                    State = user.State.ToUpper(),
                    ImagemURL = user.ImagemURL,
                    StatusMessage = StatusMessage
                };

                return View(userNovo);
            }
        }

        //POST - UPSERT
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async System.Threading.Tasks.Task<IActionResult> UpsertAsync(ApplicationUserViewModel user)
        {
            string returnUrl = Url.Content("~/");
            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

            ApplicationUser userInserir = new();

            if (user.Id != null)
            {
                userInserir = _db.ApplicationUser.Find(user.Id);
            }
            else
            {
                userInserir.UserName = user.Email;
                userInserir.Email = user.Email;
            }

            userInserir.FullName = user.FullName;
            userInserir.PhoneNumber = user.PhoneNumber;
            userInserir.Birthdate = user.Birthdate;
            userInserir.City = user.City;
            userInserir.State = user.State.ToUpper();

            if (user.ImagemUpload != null)
            {
                var imgPrefixo = Guid.NewGuid() + "_";

                if (!Util.Util.UploadArquivo(user.ImagemUpload, imgPrefixo))
                {
                    return View(user);
                }
                userInserir.ImagemURL = imgPrefixo + user.ImagemUpload.FileName;
            }

            if (userInserir.ImagemURL == null)
                userInserir.ImagemURL = "0c60afc4-b8d0-4cc8-916d-d56a70b6e0db_unnamed.png";

            if (user.Id == null)
            {
                //Creating
                var result = await _userManager.CreateAsync(userInserir, user.Password);

                if (result.Succeeded)
                {
                    if (User.IsInRole(WC.AdminRole))
                    {
                        //an admin has logged in and they try to create a new user
                        await _userManager.AddToRoleAsync(userInserir, WC.AdminRole);
                    }
                    else
                    {
                        await _userManager.AddToRoleAsync(userInserir, WC.CustomerRole);
                    }

                    var code = await _userManager.GenerateEmailConfirmationTokenAsync(userInserir);
                    code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
                    var callbackUrl = Url.Page(
                        "/Account/ConfirmEmail",
                        pageHandler: null,
                        values: new { area = "Identity", userId = user.Id, code, returnUrl },
                        protocol: Request.Scheme);

                    await _emailSender.SendEmailAsync(userInserir.Email, "Confirme seu email",
                        $"Por favor, confirme sua conta <a href='{HtmlEncoder.Default.Encode(callbackUrl)}'>clicando aqui</a>.");

                    if (_userManager.Options.SignIn.RequireConfirmedAccount)
                    {
                        return RedirectToPage("RegisterConfirmation", new { email = userInserir.Email, returnUrl });
                    }
                    else
                    {
                        if (!User.IsInRole(WC.AdminRole))
                        {
                            await _signInManager.SignInAsync(userInserir, isPersistent: false);
                        }
                        else
                        {
                            return RedirectToAction("Index");
                        }
                        return LocalRedirect(returnUrl);
                    }
                }
                foreach (var error in result.Errors)
                {
                    error.Description = error.Description.Replace("Passwords must have at least one non alphanumeric character.", "As senhas devem ter pelo menos um caractere não alfanumérico.");
                    error.Description = error.Description.Replace("Passwords must have at least one lowercase ('a'-'z').", "As senhas devem ter pelo menos uma minúscula ('a' - 'z')");
                    error.Description = error.Description.Replace("Passwords must have at least one uppercase ('A'-'Z').", "As senhas devem ter pelo menos uma maiúscula ('A' - 'Z').");
                    error.Description = error.Description.Replace("Username", "O E-mail");
                    error.Description = error.Description.Replace("is already taken.", "já está em uso.");
                    ModelState.AddModelError(string.Empty, error.Description);
                }
                return View(user);
            }
            else
            {
                //updating
                _db.ApplicationUser.Update(userInserir);
                _db.SaveChanges();

                returnUrl = Url.Content($"~/ApplicationUser/Details/{userInserir.Id}");
                StatusMessage = "Alterações realizadas com sucesso!";
            }

            return LocalRedirect(returnUrl);
        }


        //GET - Details
        public IActionResult Details(string id)
        {
            string userId = _userManager.GetUserId(User);

            ApplicationUser user = _db.ApplicationUser.Where(u => u.Id == id).FirstOrDefault();

            List<Amigo> amigos = _db.Amigos.Where(a => a.UserId == userId || a.AmigoId == userId).ToList();

            var friendCurrentUserIds = amigos.Where(u => u.Id.ToString() != userId).Select(a => a.AmigoId);
            friendCurrentUserIds = friendCurrentUserIds.Concat(amigos.Where(u => u.Id.ToString() != userId).Select(a => a.UserId));

            IEnumerable<ApplicationUser> users = _db.ApplicationUser.ToList().Where(u => !friendCurrentUserIds.Contains(u.Id));

            amigos = _db.Amigos.Where(a => a.Ativo).ToList();

            var friendIds = amigos.Where(amigo => amigo.UserId == id).Select(amigo => amigo.AmigoId);
            friendIds = friendIds.Concat(amigos.Where(amigo => amigo.AmigoId == id).Select(amigo => amigo.UserId));

            IEnumerable<ApplicationUser> usersAmigos = _db.ApplicationUser.ToList().Where(user => friendIds.Contains(user.Id));

            int amigosComumQtd = GetAmigosComum(id).Count;

            List<Avaliacao> avalicoes = _db.Avaliacoes
                                           .Include(a => a.Filme)
                                           .Include(a => a.Livro)
                                           .Include(a => a.Serie)
                                           .Include(a => a.User)
                                           .Where(a => a.UserId == id)
                                           .ToList();

            ListagemViewModel retorno = new()
            {
                Users = users.ToList(),
                Amigos = usersAmigos.ToList(),
                User = user,
                Avaliacoes = avalicoes,
                AmigosComumQtd = amigosComumQtd
            };

            user.StatusMessage = StatusMessage;

            return View(retorno);
        }

        //GET - DELETE
        public IActionResult Delete(string id)
        {
            if (id == null)
            {
                return NotFound();
            }
            ApplicationUser user = _db.ApplicationUser.Find(id);

            if (user == null)
            {
                return NotFound();
            }

            return View(user);
        }

        //POST - DELETE
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public IActionResult DeletePost(string id)
        {
            var obj = _db.ApplicationUser.Find(id);
            if (obj == null)
            {
                return NotFound();
            }

            _db.ApplicationUser.Remove(obj);
            _db.SaveChanges();
            return RedirectToAction("Index");
        }

        //POST - ENVIAR SOLICITACAO AMIZADE
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async System.Threading.Tasks.Task<IActionResult> AdicionarAmigo(string id)
        {
            string returnUrl = Url.Content($"~/ApplicationUser/Details/{id}");

            ApplicationUser currentUser = _db.ApplicationUser.Where(u => u.Id == _userManager.GetUserId(User)).FirstOrDefault();
            ApplicationUser friend = _db.ApplicationUser.Where(u => u.Id == id).FirstOrDefault();

            Amigo amigo = new()
            {
                UserId = currentUser.Id,
                AmigoId = id
            };

            if (ModelState.IsValid)
            {
                //Creating
                _db.Amigos.Add(amigo);
                _db.SaveChanges();
            }

            StatusMessage = "Pedido de amizade enviado com sucesso!";

            await _emailSender.SendEmailAsync(friend.Email, "RAJE - Solicitação de Amizade",
                        $"Você acabou de receber uma solicitação de amizade do membro {currentUser.FullName}, saiba mais <a href='https://raje.azurewebsites.net/ApplicationUser/SolicitacoesAmizade'>clicando aqui</a>.");

            return LocalRedirect(returnUrl);
        }

        //POST - ACEITAR SOLICITACAO AMIZADE
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async System.Threading.Tasks.Task<IActionResult> AceitarAmigo(string id)
        {
            string returnUrl = Url.Content($"~/ApplicationUser/SolicitacoesAmizade");

            ApplicationUser currentUser = _db.ApplicationUser.Where(u => u.Id == _userManager.GetUserId(User)).FirstOrDefault();
            ApplicationUser friend = _db.ApplicationUser.Where(u => u.Id == id).FirstOrDefault();

            Amigo amigo = _db.Amigos.ToList()
                                    .Where(amigo =>
                                           amigo.Ativo == false &&
                                           amigo.UserId == id &&
                                           amigo.AmigoId == currentUser.Id)
                                    .FirstOrDefault();

            if (ModelState.IsValid)
            {
                amigo.Ativo = true;
                _db.Amigos.Update(amigo);
                _db.SaveChanges();
            }

            await _emailSender.SendEmailAsync(friend.Email, "RAJE - Solicitação de Amizade",
                        $"O membro {currentUser.FullName} acabou de aceitar sua solicitação de amizade, saiba mais <a href='https://raje.azurewebsites.net/ApplicationUser/Details/{currentUser.Id}'>clicando aqui</a>.");

            StatusMessage = "Pedido de amizade aceito com sucesso!";
            return LocalRedirect(returnUrl);
        }

        //POST - RECUSAR SOLICITACAO AMIZADE
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult RecusarAmigo(string id)
        {
            string returnUrl = Url.Content($"~/ApplicationUser/SolicitacoesAmizade");
            string currentUserId = _userManager.GetUserId(User);

            Amigo amigo = _db.Amigos.ToList()
                                    .Where(amigo =>
                                           amigo.Ativo == false &&
                                           amigo.UserId == id &&
                                           amigo.AmigoId == currentUserId)
                                    .FirstOrDefault();

            if (ModelState.IsValid)
            {
                _db.Amigos.Remove(amigo);
                _db.SaveChanges();
            }

            StatusMessage = "Pedido de amizade aceito com sucesso!";

            return LocalRedirect(returnUrl);
        }

        //GET - AMIGO
        public IActionResult Amigos(string id, string nome)
        {
            if (id == null)
            {
                id = _userManager.GetUserId(User);
            }

            ApplicationUser user = _db.ApplicationUser.Where(u => u.Id == id).FirstOrDefault();

            IEnumerable<Amigo> amigos = _db.Amigos.Where(a => a.Ativo).ToList();

            var amigoIds = amigos.Where(amigo => amigo.UserId == id).Select(amigo => amigo.AmigoId);
            amigoIds = amigoIds.Concat(amigos.Where(amigo => amigo.AmigoId == id).Select(amigo => amigo.UserId));

            IEnumerable<ApplicationUser> users = _db.ApplicationUser.ToList().Where(user => amigoIds.Contains(user.Id));

            if (nome != null)
                users = users.Where(user => user.FullName.ToLower().Contains(nome.ToLower()));


            ListagemViewModel retorno = new()
            {
                Users = users.ToList(),
                User = user
            };

            return View(retorno);
        }

        //GET - SOLICITACOES
        public IActionResult SolicitacoesAmizade(string nome)
        {
            string currentUserId = _userManager.GetUserId(User);

            IEnumerable<Amigo> amigos = _db.Amigos.ToList().Where(amigo => amigo.Ativo == false);

            var amigoIds = amigos.Where(amigo => amigo.AmigoId == currentUserId).Select(amigo => amigo.UserId);

            IEnumerable<ApplicationUser> users = _db.ApplicationUser.ToList().Where(user => amigoIds.Contains(user.Id));

            if (nome != null)
                users = users.Where(user => user.FullName.ToLower().Contains(nome.ToLower()));

            return View(users);
        }

        //GET - AMIGO EM COMUM
        public IActionResult AmigosComum(string id, string nome)
        {
            string currentUserId = _userManager.GetUserId(User);

            var comumFriends = GetAmigosComum(id);

            ApplicationUser user = _db.ApplicationUser.Where(u => u.Id == id).FirstOrDefault();

            if (nome != null)
                comumFriends = comumFriends.Where(user => user.FullName.ToLower().Contains(nome.ToLower())).ToList();


            ListagemViewModel retorno = new()
            {
                Users = comumFriends.ToList(),
                User = user
            };

            return View(retorno);
        }

        //GET - AMIGO EM COMUM
        public List<ApplicationUser> GetAmigosComum(string id)
        {
            string currentUserId = _userManager.GetUserId(User);

            List<Amigo> friendsByCurrentUser = _db.Amigos.Where(a => a.Ativo && (a.UserId == currentUserId || a.AmigoId == currentUserId)).ToList();

            var friendCurrentUserIds = friendsByCurrentUser.Where(u => u.Id.ToString() != currentUserId).Select(a => a.AmigoId);
            friendCurrentUserIds = friendCurrentUserIds.Concat(friendsByCurrentUser.Where(u => u.Id.ToString() != currentUserId).Select(a => a.UserId));

            List<Amigo> friends = _db.Amigos.Where(a => a.Ativo && (a.UserId == id || a.AmigoId == id)).ToList();
            var friendIds = friends.Where(u => u.Id.ToString() != id).Select(a => a.AmigoId);
            friendIds = friendIds.Concat(friends.Where(u => u.Id.ToString() != id).Select(a => a.UserId));

            var comumFriendIds = friendCurrentUserIds.Where(id => friendIds.Contains(id));

            var comumFriends = _db.ApplicationUser.Where(u => u.Id != currentUserId && u.Id != id && comumFriendIds.Contains(u.Id));

            return comumFriends.ToList();
        }
    }
}