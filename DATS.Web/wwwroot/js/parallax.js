class ParallaxManager {
    constructor() {
        console.log('Initializing Parallax Background');
        
        this.canvas = document.createElement('canvas');
        this.ctx = this.canvas.getContext('2d');
        this.layers = [];
        this.time = 0;
        this.driftSpeed = 0.05;
        this.imageLoaded = false;

        this.canvas.style.backgroundColor = '#000000';
        
        this.resize();
        window.addEventListener('resize', this.resize.bind(this));
        
        document.body.insertBefore(this.canvas, document.body.firstChild);
        this.canvas.style.position = 'fixed';
        this.canvas.style.top = '0';
        this.canvas.style.left = '0';
        this.canvas.style.zIndex = '-1';

        this.createLayers();
        this.initStars();
        
        this.animate();
    }

    resize() {
        console.log('Resizing canvas:', window.innerWidth, window.innerHeight);
        this.canvas.width = window.innerWidth;
        this.canvas.height = window.innerHeight;
        this.initStars(true);
    }

    createLayers() {
        console.log('Creating parallax layers');
        
        const imgLayer = {
            type: 'image',
            image: new Image(),
            speed: 0.2,
            offset: 0
        };
        
        imgLayer.image.crossOrigin = "anonymous";
        imgLayer.image.onload = () => {
            console.log('Space texture loaded');
            this.imageLoaded = true;
        };
        imgLayer.image.onerror = (e) => {
            console.error('Error loading space texture:', e);
            this.imageLoaded = false;
        };
        imgLayer.image.src = "/media/parallax.png";
        this.layers.push(imgLayer);

        this.layers.push(this.createStarLayer({
            id: 'far_stars',
            color: "#4B5072",
            count: 1500,
            speed: 0.6,
            seed: 3909,
            size: 1
        }));

        this.layers.push(this.createStarLayer({
            id: 'mid_stars',
            color: "#7E86BF",
            count: 800,
            speed: 1.2,
            seed: 6843,
            size: 1.2
        }));

        this.layers.push(this.createStarLayer({
            id: 'close_stars',
            color: "00D363",
            count: 400,
            speed: 2.0,
            seed: 3748,
            size: 1.5
        }));
    }

    createStarLayer(config) {
        return {
            type: 'stars',
            points: [],
            ...config,
            init: function(canvas) {
                const rand = this.seededRandom(this.seed);
                this.points = Array.from({length: this.count}, () => ({
                    x: rand() * canvas.width * 2,
                    y: rand() * canvas.height,
                    alpha: Math.pow(rand(), 0.5)
                }));
            },
            seededRandom: function(seed) {
                let x = Math.sin(seed) * 10000;
                return () => (x = Math.sin(x) * 10000, x - Math.floor(x));
            }
        };
    }

    initStars(resize = false) {
        console.log('Initializing stars');
        this.layers.forEach(layer => {
            if (layer.type === 'stars') {
                if (resize || !layer.points.length) {
                    layer.init(this.canvas);
                }
            }
        });
    }

    animate() {
        try {
            this.ctx.fillStyle = '#000000';
            this.ctx.fillRect(0, 0, this.canvas.width, this.canvas.height);
            
            this.time += this.driftSpeed;
            
            this.layers.forEach(layer => {
                if (layer.type === 'image') {
                    this.drawImageLayer(layer);
                } else if (layer.type === 'stars') {
                    this.drawStarLayer(layer);
                }
            });
            
            requestAnimationFrame(this.animate.bind(this));
        } catch (e) {
            console.error('Animation error:', e);
            this.ctx.fillStyle = '#000000';
            this.ctx.fillRect(0, 0, this.canvas.width, this.canvas.height);
        }
    }

    drawImageLayer(layer) {
        if (!this.imageLoaded) return;
        
        const offset = (this.time * layer.speed) % layer.image.width;
        this.ctx.save();
        this.ctx.globalAlpha = 0.8;
        this.ctx.drawImage(layer.image, -offset, 0);
        this.ctx.drawImage(layer.image, layer.image.width - offset, 0);
        this.ctx.restore();
    }

    drawStarLayer(layer) {
        const offset = (this.time * layer.speed) % this.canvas.width;
        
        layer.points.forEach(point => {
            const x = (point.x - offset) % (this.canvas.width * 2);
            const y = point.y % this.canvas.height;
            
            this.ctx.beginPath();
            this.ctx.fillStyle = layer.color;
            this.ctx.globalAlpha = point.alpha;
            this.ctx.arc(
                x < 0 ? x + this.canvas.width * 2 : x,
                y,
                layer.size,
                0,
                Math.PI * 2
            );
            this.ctx.fill();
        });
    }
}

document.addEventListener('DOMContentLoaded', () => {
    try {
        new ParallaxManager();
    } catch (e) {
        console.error('Failed to initialize parallax:', e);
    }
});