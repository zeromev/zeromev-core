window.orderBar = (r, g, b) => {
    var c = document.getElementById("orderCanvas");
    var ctx = c.getContext("2d");
    ctx.clearRect(0, 0, c.width, c.height);
    var len = r.length;
    var h = c.height;
    for (var i = 0; i < len; i++) {
        ctx.beginPath();
        ctx.strokeStyle = "rgb(" + r[i] + "," + g[i] + "," + b[i] + ")";
        ctx.moveTo(i, 0);
        ctx.lineTo(i, h);
        ctx.stroke();
    }
};