// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

document.addEventListener("DOMContentLoaded", function () {
    // Preloader Logic
    const preloader = document.getElementById('spa-preloader');
    if (preloader) {
        window.addEventListener('load', function () {
            setTimeout(function() {
                preloader.style.transition = 'opacity 0.5s ease';
                preloader.style.opacity = '0';
                setTimeout(() => {
                    preloader.style.display = 'none';
                    // Ensure no pointer events after hiding
                    preloader.style.pointerEvents = 'none';
                }, 500);
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

    // Lazy load images
    document.querySelectorAll('img[data-src]').forEach(img => {
        img.setAttribute('src', img.getAttribute('data-src'));
        img.onload = () => img.removeAttribute('data-src');
    });

    // Fade-in sections when scroll
    const observer = new IntersectionObserver((entries) => {
        entries.forEach(entry => {
            if (entry.isIntersecting) {
                entry.target.classList.add('visible');
                observer.unobserve(entry.target);
            }
        });
    }, { threshold: 0.1 });

    document.querySelectorAll('.section-fade').forEach(section => {
        observer.observe(section);
    });
});