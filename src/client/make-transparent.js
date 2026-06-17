const Jimp = require('jimp');

async function makeBackgroundTransparent() {
    const image = await Jimp.read('./images/icon.png');
    
    // Get the background color (dark gray - approximately #3C3C3C or rgb(60,60,60))
    // We'll make all pixels close to this color transparent
    image.scan(0, 0, image.bitmap.width, image.bitmap.height, function(x, y, idx) {
        const red = this.bitmap.data[idx + 0];
        const green = this.bitmap.data[idx + 1];
        const blue = this.bitmap.data[idx + 2];
        const alpha = this.bitmap.data[idx + 3];
        
        // Check if the pixel is close to the dark gray background color
        // Allow some tolerance for anti-aliasing
        if (red < 80 && green < 80 && blue < 80 && 
            Math.abs(red - green) < 20 && Math.abs(red - blue) < 20) {
            // Make it transparent
            this.bitmap.data[idx + 3] = 0;
        }
    });
    
    await image.writeAsync('./images/icon.png');
    console.log('Background made transparent successfully!');
}

makeBackgroundTransparent().catch(console.error);
