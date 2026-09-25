// Proof-of-work pentru pagina de inregistrare: cauta un numar n pentru care
// SHA-256(token + ":" + n) incepe cu `bits` biti de zero. Ruleaza intr-un Web Worker, ca
// pagina sa ramana fluida; SHA-256 e implementat aici pentru ca crypto.subtle e asincron
// (prea lent pentru sute de mii de apeluri) si lipseste pe http simplu.
'use strict';

var K = new Uint32Array([
    0x428a2f98, 0x71374491, 0xb5c0fbcf, 0xe9b5dba5, 0x3956c25b, 0x59f111f1, 0x923f82a4, 0xab1c5ed5,
    0xd807aa98, 0x12835b01, 0x243185be, 0x550c7dc3, 0x72be5d74, 0x80deb1fe, 0x9bdc06a7, 0xc19bf174,
    0xe49b69c1, 0xefbe4786, 0x0fc19dc6, 0x240ca1cc, 0x2de92c6f, 0x4a7484aa, 0x5cb0a9dc, 0x76f988da,
    0x983e5152, 0xa831c66d, 0xb00327c8, 0xbf597fc7, 0xc6e00bf3, 0xd5a79147, 0x06ca6351, 0x14292967,
    0x27b70a85, 0x2e1b2138, 0x4d2c6dfc, 0x53380d13, 0x650a7354, 0x766a0abb, 0x81c2c92e, 0x92722c85,
    0xa2bfe8a1, 0xa81a664b, 0xc24b8b70, 0xc76c51a3, 0xd192e819, 0xd6990624, 0xf40e3585, 0x106aa070,
    0x19a4c116, 0x1e376c08, 0x2748774c, 0x34b0bcb5, 0x391c0cb3, 0x4ed8aa4a, 0x5b9cca4f, 0x682e6ff3,
    0x748f82ee, 0x78a5636f, 0x84c87814, 0x8cc70208, 0x90befffa, 0xa4506ceb, 0xbef9a3f7, 0xc67178f2
]);
var W = new Uint32Array(64);
var H = new Uint32Array(8);

function rotr(x, n) { return (x >>> n) | (x << (32 - n)); }

// Intoarce primul cuvant de 32 de biti al hash-ului si il pune complet in H.
function sha256(bytes, length) {
    H[0] = 0x6a09e667; H[1] = 0xbb67ae85; H[2] = 0x3c6ef372; H[3] = 0xa54ff53a;
    H[4] = 0x510e527f; H[5] = 0x9b05688c; H[6] = 0x1f83d9ab; H[7] = 0x5be0cd19;
    var total = ((length + 9 + 63) >> 6) << 6;
    for (var i = length; i < total; i++) { bytes[i] = 0; }
    bytes[length] = 0x80;
    var bitLength = length * 8;
    bytes[total - 4] = (bitLength >>> 24) & 0xff;
    bytes[total - 3] = (bitLength >>> 16) & 0xff;
    bytes[total - 2] = (bitLength >>> 8) & 0xff;
    bytes[total - 1] = bitLength & 0xff;

    for (var offset = 0; offset < total; offset += 64) {
        for (var t = 0; t < 16; t++) {
            var j = offset + t * 4;
            W[t] = (bytes[j] << 24) | (bytes[j + 1] << 16) | (bytes[j + 2] << 8) | bytes[j + 3];
        }
        for (t = 16; t < 64; t++) {
            var w15 = W[t - 15], w2 = W[t - 2];
            var s0 = rotr(w15, 7) ^ rotr(w15, 18) ^ (w15 >>> 3);
            var s1 = rotr(w2, 17) ^ rotr(w2, 19) ^ (w2 >>> 10);
            W[t] = (W[t - 16] + s0 + W[t - 7] + s1) | 0;
        }
        var a = H[0], b = H[1], c = H[2], d = H[3], e = H[4], f = H[5], g = H[6], h = H[7];
        for (t = 0; t < 64; t++) {
            var S1 = rotr(e, 6) ^ rotr(e, 11) ^ rotr(e, 25);
            var ch = (e & f) ^ (~e & g);
            var t1 = (h + S1 + ch + K[t] + W[t]) | 0;
            var S0 = rotr(a, 2) ^ rotr(a, 13) ^ rotr(a, 22);
            var maj = (a & b) ^ (a & c) ^ (b & c);
            var t2 = (S0 + maj) | 0;
            h = g; g = f; f = e; e = (d + t1) | 0; d = c; c = b; b = a; a = (t1 + t2) | 0;
        }
        H[0] += a; H[1] += b; H[2] += c; H[3] += d; H[4] += e; H[5] += f; H[6] += g; H[7] += h;
    }
    return H[0];
}

function leadingZeros() {
    var bits = 0;
    for (var i = 0; i < 8; i++) {
        if (H[i] === 0) { bits += 32; continue; }
        return bits + Math.clz32(H[i]);
    }
    return bits;
}

self.onmessage = function (event) {
    var token = event.data.token, bits = event.data.bits;
    var prefix = new TextEncoder().encode(token + ':');
    var bytes = new Uint8Array(prefix.length + 32 + 72);
    bytes.set(prefix);
    var started = Date.now();
    for (var n = 0; ; n++) {
        var digits = String(n);
        var length = prefix.length;
        for (var k = 0; k < digits.length; k++) { bytes[length++] = digits.charCodeAt(k); }
        sha256(bytes, length);
        if (leadingZeros() >= bits) {
            self.postMessage({ solution: digits, tries: n + 1, ms: Date.now() - started });
            return;
        }
        if ((n & 0xffff) === 0 && n > 0) {
            self.postMessage({ progress: n });
        }
    }
};
