// ---- Slider value display ----

var PARAMS = [
    'generations', 'releaseNumber', 'mortality', 'eggsPerFemale',
    'cas9Activity', 'hdrRate', 'maternalCas9', 'conservation', 'migrationBaseRate'
];

var DECIMAL_PARAMS = {
    mortality: 2, cas9Activity: 2, hdrRate: 2, maternalCas9: 2,
    conservation: 3, migrationBaseRate: 3
};

function formatValue(id, val) {
    var dec = DECIMAL_PARAMS[id];
    return dec !== undefined ? parseFloat(val).toFixed(dec) : val;
}

function updateMigrationDetail() {
    var base = parseFloat(document.getElementById('migrationBaseRate').value);
    var parts = [];
    for (var i = 0; i < 4; i++) {
        var rate = base / Math.pow(10, i);
        parts.push('Pop ' + i + '-' + (i+1) + ': ' + rate.toFixed(Math.min(6, 3 + i)));
    }
    document.getElementById('migration-detail').textContent = parts.join('   ');
}

PARAMS.forEach(function(id) {
    var el = document.getElementById(id);
    var valEl = document.getElementById(id + '-val');
    el.addEventListener('input', function() {
        valEl.textContent = formatValue(id, el.value);
        if (id === 'migrationBaseRate') updateMigrationDetail();
    });
});

updateMigrationDetail();

// ---- Simulation launch ----

var pollTimer = null;

function getConfig() {
    var config = {};
    PARAMS.forEach(function(id) {
        var el = document.getElementById(id);
        var val = parseFloat(el.value);
        if (id === 'generations' || id === 'releaseNumber' || id === 'eggsPerFemale')
            val = parseInt(el.value);
        config[id] = val;
    });
    return config;
}

function runSimulation() {
    var btn = document.getElementById('btn-run');
    var viewBtn = document.getElementById('btn-view');
    btn.disabled = true;
    btn.textContent = 'Running...';
    viewBtn.classList.add('disabled');

    document.getElementById('progress-container').style.display = 'block';
    document.getElementById('progress-fill').style.width = '0%';
    document.getElementById('progress-text').textContent = 'Starting simulation...';

    fetch('/api/simulate', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(getConfig())
    })
    .then(function(res) { return res.json(); })
    .then(function(data) {
        if (data.ok) {
            pollTimer = setInterval(pollStatus, 1000);
        } else {
            btn.disabled = false;
            btn.textContent = 'Run Simulation';
            document.getElementById('progress-text').textContent = 'Error: ' + (data.error || 'unknown');
        }
    })
    .catch(function(err) {
        btn.disabled = false;
        btn.textContent = 'Run Simulation';
        document.getElementById('progress-text').textContent = 'Error: ' + err;
    });
}

function pollStatus() {
    fetch('/api/status')
    .then(function(res) { return res.json(); })
    .then(function(data) {
        if (!data || !data.status) return;

        var totalSteps = data.totalIterations * data.totalGenerations;
        var doneSteps = (data.iteration - 1) * data.totalGenerations + data.generation;
        if (data.status === 'completed') doneSteps = totalSteps;
        var pct = totalSteps > 0 ? Math.round(100 * doneSteps / totalSteps) : 0;

        document.getElementById('progress-fill').style.width = pct + '%';
        document.getElementById('progress-text').textContent =
            'Iteration ' + data.iteration + '/' + data.totalIterations +
            ', Generation ' + data.generation + '/' + data.totalGenerations;

        if (data.status === 'completed') {
            clearInterval(pollTimer);
            document.getElementById('btn-run').disabled = false;
            document.getElementById('btn-run').textContent = 'Run Simulation';
            document.getElementById('progress-text').textContent = 'Simulation complete!';
            document.getElementById('btn-view').classList.remove('disabled');
        }
    })
    .catch(function() {});
}
