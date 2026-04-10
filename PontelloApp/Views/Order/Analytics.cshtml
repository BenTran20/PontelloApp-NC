@{
    ViewData["Title"] = "Analytics";

    var topProducts = ViewBag.TopProducts as IEnumerable<dynamic>;
    var revenueTrends = ViewBag.RevenueTrends as IEnumerable<dynamic>;
}

<div class="container-fluid">

    <div class="d-flex justify-content-between align-items-center mb-4">
        <h2 class="fw-bold">Analytics Dashboard</h2>
        <a asp-action="Admin" class="btn btn-outline-secondary">← Back to Dashboard</a>
    </div>

    <!-- FILTERS -->
    <div class="card shadow-sm mb-4">
        <div class="card-body d-flex gap-3 flex-wrap">

            <div>
                <label>From</label>
                <input type="date" id="fromDate" class="form-control" value="@ViewData["FromDate"]" />
            </div>

            <div>
                <label>To</label>
                <input type="date" id="toDate" class="form-control" value="@ViewData["ToDate"]" />
            </div>

            <div class="align-self-end">
                <button id="filterBtn" class="btn btn-primary">Apply</button>
            </div>
        </div>
    </div>

    <!-- CHARTS -->
    <div class="row">

        <!-- Revenue Trend -->
        <div class="col-lg-6 mb-4">
            <div class="card shadow-sm">
                <div class="card-header fw-bold">Revenue Trend</div>
                <div class="card-body">
                    <canvas id="revenueChart"></canvas>
                </div>
            </div>
        </div>

    </div>

    <!-- TOP PRODUCTS -->
    <div class="card shadow-sm">
        <div class="card-header fw-bold">Top Selling Products</div>
        <div class="card-body p-2">
            <table class="table table-hover">
                <thead>
                    <tr>
                        <th>Product</th>
                        <th>Qty</th>
                        <th>Revenue</th>
                    </tr>
                </thead>
                <tbody>
                    @foreach (var p in topProducts)
                    {
                        <tr>
                            <td>@p.ProductName</td>
                            <td>@p.QuantitySold</td>
                            <td>@p.Revenue.ToString("C")</td>
                        </tr>
                    }
                </tbody>
            </table>
        </div>
    </div>
</div>

@section Scripts {
    <script src="https://cdn.jsdelivr.net/npm/chart.js"></script>

    <script>
        const revenueData = @Html.Raw(System.Text.Json.JsonSerializer.Serialize(revenueTrends));

        // Line chart
        new Chart(document.getElementById('revenueChart'), {
            type: 'line',
            data: {
                labels: revenueData.map(x => x.Date),
                datasets: [{
                    label: 'Revenue',
                    data: revenueData.map(x => x.Revenue),
                    borderColor: 'blue',
                    tension: 0.3
                }]
            }
        });

        // Filter
        document.getElementById('filterBtn').addEventListener('click', () => {
            const from = document.getElementById('fromDate').value;
            const to = document.getElementById('toDate').value;

            window.location.href = `/Order/Analytics?fromDate=${from}&toDate=${to}&month`;
        });
    </script>
}
