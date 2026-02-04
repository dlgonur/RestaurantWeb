(() => {

    // --------- Chart.js (Günlük Ciro Trend) ---------
    const chartEl = document.getElementById("ciroChart");
    if (chartEl && window.ciroChartData) {
        new Chart(chartEl, {
            type: 'line',
            data: {
                labels: window.ciroChartData.labels,
                datasets: [{
                    label: 'Ciro',
                    data: window.ciroChartData.data,
                    tension: 0.2
                }]
            },
            options: {
                responsive: true,
                plugins: { legend: { display: true } },
                scales: { y: { beginAtZero: true } }
            }
        });
    }

    // --------- PDF Export ---------
    const btnPdf = document.getElementById("btnPdf");
    if (!btnPdf) return;

    btnPdf.addEventListener("click", async () => {
        const report = document.getElementById("reportArea");
        if (!report) return;

        // Chart render için küçük bekleme
        await new Promise(r => setTimeout(r, 150));

        const canvas = await html2canvas(report, {
            scale: 2,
            useCORS: true
        });

        const imgData = canvas.toDataURL("image/png");

        const { jsPDF } = window.jspdf;
        const pdf = new jsPDF("p", "mm", "a4");

        const pdfWidth = pdf.internal.pageSize.getWidth();
        const pdfHeight = (canvas.height * pdfWidth) / canvas.width;

        pdf.addImage(imgData, "PNG", 0, 10, pdfWidth, pdfHeight);

        const start = report.dataset.start;
        const end = report.dataset.end;

        pdf.save(`Dashboard_${start}_${end}.pdf`);
    });

})();
