// Shared behaviour for every drive model's configuration page.
//
// Each page declares, before loading this script:
//   SIM_MODEL   — the model id sent to the API and used to route results
//   SIM_PARAMS  — the slider ids that make up that model's configuration
//
// Everything below is driven by those two values, so adding a model needs no
// changes here.

// Number of decimal places to show for each slider. Sliders not listed are
// integers and are shown as-is.
var DECIMAL_PARAMS = {
    mortality: 2,
    fitnessCost: 2,
    cas9Activity: 2,
    hdrRate: 2,
    maternalCas9: 2,
    conservation: 5,
    xShredRate: 2,
    yDriveFertility: 2,
    medeaPenetrance: 2,
    medeaRescue: 2,
    medeaDistance: 3,
    migrationBaseRate: 3
};

// Sliders whose value must be sent as an integer rather than a float.
var INTEGER_PARAMS = ['generations', 'releaseNumber', 'eggsPerFemale'];

function formatValue(id, val) {
    var dec = DECIMAL_PARAMS[id];
    return dec !== undefined ? parseFloat(val).toFixed(dec) : val;
}

function paramValue(id) {
    var el = document.getElementById(id);
    return el ? parseFloat(el.value) : null;
}

// ---- Derived readouts ----
//
// Each of these annotates a slider with the quantity the user actually cares
// about, which is not always the raw parameter. They are no-ops on pages that
// do not have the relevant slider.

function updateMigrationDetail() {
    var el = document.getElementById('migration-detail');
    if (!el) return;

    var base = paramValue('migrationBaseRate');
    var parts = [];
    for (var i = 0; i < 4; i++) {
        var rate = base / Math.pow(10, i);
        parts.push('Pop ' + (i + 1) + '-' + (i + 2) + ': ' + rate.toFixed(Math.min(6, 3 + i)));
    }
    el.textContent = parts.join('   ');
}

function updateShredDetail() {
    var el = document.getElementById('shred-detail');
    if (!el) return;

    // Destroying a fraction d of X-bearing gametes leaves (1-d) X per 1 Y,
    // so the chance a surviving gamete carries the Y is 1 / (2 - d).
    var d = paramValue('xShredRate');
    var pY = 1 / (2 - d);
    el.textContent = 'Resulting progeny: ' + (100 * pY).toFixed(1) + '% male, ' +
                     (100 * (1 - pY)).toFixed(1) + '% female';
}

function updateLinkageDetail() {
    var el = document.getElementById('linkage-detail');
    if (!el) return;

    var d = paramValue('medeaDistance');
    if (d === 0) {
        el.textContent = 'Perfectly linked: toxin and rescue always inherited together';
    } else {
        el.textContent = 'Recombination between toxin and rescue: ' +
                         (100 * Math.min(d, 0.5)).toFixed(1) + '% of gametes';
    }
}

function updateFitnessDetail() {
    var el = document.getElementById('fitness-detail');
    if (!el) return;

    // The cost is charged per insertion, so survival is (1-c)^copies.
    var c = paramValue('fitnessCost');
    if (c === 0) {
        el.textContent = 'No fitness cost: carriers survive as well as wild types';
    } else {
        var het = 100 * c;
        var hom = 100 * (1 - Math.pow(1 - c, 2));
        el.textContent = 'Extra mortality per generation: ' + het.toFixed(0) +
                         '% if heterozygous, ' + hom.toFixed(0) + '% if homozygous';
    }
}

function updateDerivedDetails() {
    updateMigrationDetail();
    updateFitnessDetail();
    updateShredDetail();
    updateLinkageDetail();
}

// ---- Slider wiring ----

SIM_PARAMS.forEach(function(id) {
    var el = document.getElementById(id);
    var valEl = document.getElementById(id + '-val');
    if (!el || !valEl) return;

    el.addEventListener('input', function() {
        valEl.textContent = formatValue(id, el.value);
        updateDerivedDetails();
    });
});

updateDerivedDetails();

// ---- Simulation launch ----

var pollTimer = null;

function getConfig() {
    var config = { model: SIM_MODEL };
    SIM_PARAMS.forEach(function(id) {
        var el = document.getElementById(id);
        if (!el) return;
        config[id] = INTEGER_PARAMS.indexOf(id) >= 0
            ? parseInt(el.value, 10)
            : parseFloat(el.value);
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
    fetch('/api/status?model=' + encodeURIComponent(SIM_MODEL))
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
