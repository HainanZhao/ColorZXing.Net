export function drawPixels(canvas, width, height, pixels) {
    canvas.width = width;
    canvas.height = height;
    const context = canvas.getContext("2d", { alpha: false });
    const clamped = new Uint8ClampedArray(pixels.buffer, pixels.byteOffset, pixels.byteLength);
    context.putImageData(new ImageData(clamped, width, height), 0, 0);
}

export async function loadSelectedImage(input, canvas, maxDimension, maxBytes) {
    const file = input.files?.[0];
    if (!file) throw new Error("Choose an image first.");
    if (file.size > maxBytes) throw new Error("That image is larger than the 12 MB limit.");

    const bitmap = await createImageBitmap(file);
    const scale = Math.min(1, maxDimension / Math.max(bitmap.width, bitmap.height));
    const width = Math.max(1, Math.round(bitmap.width * scale));
    const height = Math.max(1, Math.round(bitmap.height * scale));
    canvas.width = width;
    canvas.height = height;
    const context = canvas.getContext("2d", { alpha: false, willReadFrequently: true });
    context.fillStyle = "#fff";
    context.fillRect(0, 0, width, height);
    context.drawImage(bitmap, 0, 0, width, height);
    bitmap.close();
    return { width, height, name: file.name, size: file.size };
}

export function readPixels(canvas) {
    const context = canvas.getContext("2d", { alpha: false, willReadFrequently: true });
    const pixels = context.getImageData(0, 0, canvas.width, canvas.height).data;
    return new Uint8Array(pixels.buffer, pixels.byteOffset, pixels.byteLength);
}

export function downloadCanvas(canvas, filename) {
    canvas.toBlob(blob => {
        const url = URL.createObjectURL(blob);
        const link = document.createElement("a");
        link.href = url;
        link.download = filename;
        link.click();
        URL.revokeObjectURL(url);
    }, "image/png");
}
