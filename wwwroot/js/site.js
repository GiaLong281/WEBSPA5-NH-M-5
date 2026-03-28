// SpaN5 Premium JS Logic

document.addEventListener("DOMContentLoaded", function () {
    // 1. Preloader Logic
    const preloader = document.getElementById('spa-preloader');
    if (preloader) {
        // Hide preloader after everything (including videos) is loaded
        window.addEventListener('load', function () {
            setTimeout(function() {
                preloader.classList.add('fade-out');
            }, 600); 
        });
    }

    // 2. Navbar Scroll Logic
    const navbar = document.querySelector('.spa-navbar');
    if (navbar) {
        const handleScroll = () => {
            if (window.scrollY > 50) {
                navbar.classList.add('scrolled');
            } else {
                navbar.classList.remove('scrolled');
            }
        };

        window.addEventListener('scroll', handleScroll);
        handleScroll(); // Initial check
    }



    // 4. Smooth Scrolling for Anchor Links
    document.querySelectorAll('a[href^="#"]').forEach(anchor => {
        anchor.addEventListener('click', function (e) {
            e.preventDefault();
            const target = document.querySelector(this.getAttribute('href'));
            if (target) {
                const navHeight = 76;
                const targetPosition = target.getBoundingClientRect().top + window.pageYOffset - navHeight;
                window.scrollTo({
                    top: targetPosition,
                    behavior: 'smooth'
                });
            }
        });
    });
});
