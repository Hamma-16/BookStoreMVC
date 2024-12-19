using BookStoreMVC.DataAccess.Repository.IRepository;
using BookStoreMVC.Models;
using BookStoreMVC.Models.ViewModels;
using BookStoreMVC.Services;
using BookStoreMVC.Utility;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;


/// <summary>
/// Contrôleur gérant les opérations CRUD pour les produits (livres) dans l'espace admin
/// Utilise le pattern Repository pour l'accès aux données et le pattern UnitOfWork pour la gestion des transactions
/// </summary>
[Area("Admin")]
[Authorize(Roles = SD.Role_Admin)] // Sécurisation de l'accès aux administrateurs uniquement
public class ProductController : Controller
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IWebHostEnvironment _webHostEnvironment;
    private readonly IImageService _imageService;

    /// <summary>
    /// Constructeur du contrôleur de produits
    /// Injection des dépendances nécessaires pour travailler avec les produits
    /// </summary>
    /// <param name="unitOfWork">Service de gestion des opérations de données</param>
    /// <param name="webHostEnvironment">Environnement d'hébergement web pour la gestion des fichiers</param>
    /// <param name="imageService">Service de gestion des images</param>
    public ProductController(IUnitOfWork unitOfWork, IWebHostEnvironment webHostEnvironment, IImageService imageService)
    {
        _unitOfWork = unitOfWork;
        _webHostEnvironment = webHostEnvironment;
        _imageService = imageService;
    }

    /// <summary>
    /// Action pour afficher la liste de tous les produits
    /// Récupère tous les produits avec leurs catégories associées
    /// </summary>
    /// <returns>Vue avec la liste des produits</returns>
    public IActionResult Index()
    {
        List<Product> products = _unitOfWork.Product.GetAll(includeProperties: "Category").ToList();
        return View(products);
    }

    /// <summary>
    /// Action pour créer ou mettre à jour un produit (Upsert - Update/Insert)
    /// Gère à la fois la création et la modification des produits
    /// </summary>
    /// <param name="id">Identifiant du produit (optionnel)</param>
    /// <returns>Vue de création/modification de produit</returns>
    public IActionResult Upsert(int? id)
    {
        // Logique de préparation du modèle pour la vue
        ProductVM productVM = new()
        {
            CategoryList = _unitOfWork.Category.GetAll().Select(u => new SelectListItem
            {
                Text = u.Name,
                Value = u.Id.ToString()
            }),
            Product = new Product(),
        };

        if (id == null || id == 0)
        {
            // Création d'un nouveau produit
            return View(productVM);
        }
        else
        {
            // Mise à jour d'un produit existant
            productVM.Product = _unitOfWork.Product.GetById((int)id);
            return View(productVM);
        }
    }

    /// <summary>
    /// Action pour créer ou mettre à jour un produit (Upsert - Update/Insert)
    /// Gère à la fois la création et la modification des produits
    /// </summary>
    /// <param name="productVM">Modèle de produit</param>
    /// <param name="file">Fichier image (optionnel)</param>
    /// <returns>Vue de création/modification de produit</returns>
    [HttpPost]
    public IActionResult Upsert(ProductVM productVM, IFormFile? file)
    {
        if (ModelState.IsValid)
        {
            if (file != null)
            {
                string wwwRootPath = _webHostEnvironment.WebRootPath;
                string productPath = Path.Combine(wwwRootPath, @"images\product\");
                _imageService.DeleteIfExists(wwwRootPath, productVM.Product.ImageUrl);
                string fileName = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);
                using (var fileStream = new FileStream(Path.Combine(productPath, fileName), FileMode.Create))
                {
                    file.CopyTo(fileStream);
                }
                productVM.Product.ImageUrl = @"\images\product\" + fileName;
            }

            if (productVM.Product.Id == 0)
            {
                _unitOfWork.Product.Add(productVM.Product);
            }
            else
            {
                _unitOfWork.Product.Update(productVM.Product);
            }

            _unitOfWork.Save();
            TempData["success"] = productVM.Product.Id == 0
                ? "Product added successfully"
                : "Product updated successfully";
            return RedirectToAction("index");
        }
        else
        {
            productVM.CategoryList = _unitOfWork.Category.GetAll().Select(u => new SelectListItem
            {
                Text = u.Name,
                Value = u.Id.ToString()
            });
            return View(productVM);
        }
    }

    #region API_CALLS

    /// <summary>
    /// Action pour récupérer la liste de tous les produits
    /// </summary>
    /// <returns>Json avec la liste des produits</returns>
    [HttpGet]
    public IActionResult GetAll()
    {
        List<Product> products = _unitOfWork.Product.GetAll(includeProperties: "Category").ToList();
        return Json(new { data = products });
    }

    /// <summary>
    /// Action pour supprimer un produit
    /// </summary>
    /// <param name="id">Identifiant du produit</param>
    /// <returns>Json avec le résultat de la suppression</returns>
    [HttpDelete]
    public IActionResult Delete(int? id)
    {
        if (id == null || id == 0)
        {
            return Json(new { success = false, message = "Error while deleting" });
        }

        var productToBeDeleted = _unitOfWork.Product.GetById((int)id);

        if (productToBeDeleted == null)
        {
            return Json(new { success = false, message = "Error while deleting" });
        }

        if (productToBeDeleted.ImageUrl != null)
        {
            var oldImagePath = Path.Combine(_webHostEnvironment.WebRootPath, productToBeDeleted.ImageUrl.TrimStart('\\'));
            if (System.IO.File.Exists(oldImagePath))
            {
                System.IO.File.Delete(oldImagePath);
            }
        }

        _unitOfWork.Product.Remove(productToBeDeleted);
        _unitOfWork.Save();

        return Json(new { success = true, message = "Delete Successful" });
    }

    #endregion
}
