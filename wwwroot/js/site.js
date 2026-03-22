// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.
document.addEventListener("DOMContentLoaded", function () {
    // Preloader Logic
    const preloader = document.getElementById('spa-preloader');
    if (preloader) {
        window.addEventListener('load', function () {
            setTimeout(function() {
                preloader.classList.add('fade-out');
            }, 800); // Đợi 800ms để người dùng thấy logo
        });
    }

    const navbar = document.querySelector('.spa-navbar');
    
    // Add scroll effect for navbar
    window.addEventListener('scroll', function () {
        if (window.scrollY > 50) {
            navbar.style.boxShadow = "0 4px 20px rgba(0,0,0,0.1)";
            navbar.style.padding = "10px 0";
        } else {
            navbar.style.boxShadow = "none";
            navbar.style.padding = "15px 0";
        }
    });
});
