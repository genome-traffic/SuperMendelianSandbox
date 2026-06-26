// ---- Color and style configuration ----

const ALLELE_COLORS = {
    'WT':        '#3b82f6',
    'Transgene': '#ef4444',
    'R1':        '#22c55e',
    'R2':        '#f59e0b',
};

const FALLBACK_PALETTE = [
    '#8b5cf6', '#ec4899', '#14b8a6', '#f97316',
    '#6366f1', '#84cc16', '#06b6d4', '#e11d4b',
];

const ITER_DASHES = ['solid', 'dash', 'dot', 'dashdot', 'longdash',
                     'longdashdot', 'solid', 'dash'];

function alleleColor(allele) {
    if (ALLELE_COLORS[allele]) return ALLELE_COLORS[allele];
    var hash = 0;
    for (var i = 0; i < allele.length; i++)
        hash = ((hash << 5) - hash + allele.charCodeAt(i)) & 0x7fffffff;
    return FALLBACK_PALETTE[hash % FALLBACK_PALETTE.length];
}

function iterDash(iterIndex) {
    return ITER_DASHES[iterIndex % ITER_DASHES.length];
}

function unique(arr) {
    return [...new Set(arr)].sort((a, b) => {
        var na = Number(a), nb = Number(b);
        if (!isNaN(na) && !isNaN(nb)) return na - nb;
        return String(a).localeCompare(String(b));
    });
}

// ---- Shared layout defaults ----

var CHART_HEIGHT = 240;
var MARGIN = { t: 10, b: 38, l: 48, r: 8 };

function baseLayout(yTitle, extra) {
    var layout = {
        xaxis:  { title: { text: 'Generation', font: { size: 11 } }, tickfont: { size: 10 } },
        yaxis:  { title: { text: yTitle, font: { size: 11 } }, tickfont: { size: 10 } },
        margin: MARGIN,
        height: CHART_HEIGHT,
        legend: { font: { size: 9 }, tracegroupgap: 2 },
        hovermode: 'x unified',
    };
    if (extra) Object.assign(layout, extra);
    return layout;
}

// ---- Data helpers ----

var SPECIAL_CATEGORIES = ['Sex', 'Eggs', 'Sex_Karyotype'];

function isGenotypeRow(d) {
    return d.type === 'all' && SPECIAL_CATEGORIES.indexOf(d.category) === -1;
}

function computeAlleleFrequencies(geneRows) {
    var groups = {};
    for (var i = 0; i < geneRows.length; i++) {
        var r = geneRows[i];
        var key = r.iteration + '|' + r.generation;
        if (!groups[key]) {
            groups[key] = { iteration: r.iteration, generation: r.generation, alleles: {} };
        }
        var a = groups[key].alleles;
        a[r.value1] = (a[r.value1] || 0) + r.count;
        a[r.value2] = (a[r.value2] || 0) + r.count;
    }

    var result = [];
    var keys = Object.keys(groups);
    for (var k = 0; k < keys.length; k++) {
        var g = groups[keys[k]];
        var total = 0;
        var alleleNames = Object.keys(g.alleles);
        for (var j = 0; j < alleleNames.length; j++) total += g.alleles[alleleNames[j]];
        for (var j = 0; j < alleleNames.length; j++) {
            result.push({
                iteration:  g.iteration,
                generation: g.generation,
                allele:     alleleNames[j],
                frequency:  total > 0 ? g.alleles[alleleNames[j]] / total : 0,
            });
        }
    }
    return result;
}

// ---- Chart builders ----

function buildEggChart(div, popData, iterations, showLegend) {
    var traces = [];
    for (var i = 0; i < iterations.length; i++) {
        var iter = iterations[i];
        var rows = popData.filter(function(d) {
            return d.category === 'Eggs' && d.iteration === iter;
        }).sort(function(a, b) { return a.generation - b.generation; });

        traces.push({
            x: rows.map(function(d) { return d.generation; }),
            y: rows.map(function(d) { return d.count; }),
            name: 'Iter ' + iter,
            type: 'scatter',
            mode: 'lines',
            line: { dash: iterDash(i), width: 1.5, color: '#0f3460' },
            showlegend: showLegend,
        });
    }
    Plotly.newPlot(div, traces, baseLayout('Eggs'), { responsive: true });
}

function buildAlleleChart(div, popData, iterations, gene, showLegend) {
    var geneRows = popData.filter(function(d) {
        return d.category === gene && d.type === 'all';
    });
    var freqs = computeAlleleFrequencies(geneRows);
    var alleles = unique(freqs.map(function(d) { return d.allele; }));

    var traces = [];
    for (var a = 0; a < alleles.length; a++) {
        var allele = alleles[a];
        for (var i = 0; i < iterations.length; i++) {
            var iter = iterations[i];
            var pts = freqs.filter(function(d) {
                return d.allele === allele && d.iteration === iter;
            }).sort(function(a, b) { return a.generation - b.generation; });

            traces.push({
                x: pts.map(function(d) { return d.generation; }),
                y: pts.map(function(d) { return d.frequency; }),
                name: allele,
                type: 'scatter',
                mode: 'lines',
                line: { color: alleleColor(allele), dash: iterDash(i), width: 1.5 },
                legendgroup: allele,
                showlegend: showLegend && i === 0,
            });
        }
    }
    Plotly.newPlot(div, traces, baseLayout('Allele Freq.', { yaxis: { title: { text: 'Allele Freq.', font: { size: 11 } }, range: [0, 1.05], tickfont: { size: 10 } } }), { responsive: true });
}

function buildAdultsChart(div, popData, iterations, showLegend) {
    var sexRows = popData.filter(function(d) {
        return d.category === 'Sex' && d.type === 'all';
    });
    var traces = [];
    for (var i = 0; i < iterations.length; i++) {
        var iter = iterations[i];
        var males = sexRows.filter(function(d) {
            return d.iteration === iter && d.value1 === 'Males';
        }).sort(function(a, b) { return a.generation - b.generation; });
        var females = sexRows.filter(function(d) {
            return d.iteration === iter && d.value1 === 'Females';
        }).sort(function(a, b) { return a.generation - b.generation; });

        var gens = males.map(function(d) { return d.generation; });
        var totals = gens.map(function(gen, idx) {
            var m = males[idx] ? males[idx].count : 0;
            var f = females[idx] ? females[idx].count : 0;
            return m + f;
        });

        traces.push({
            x: gens,
            y: totals,
            name: 'Iter ' + iter,
            type: 'scatter',
            mode: 'lines',
            line: { dash: iterDash(i), width: 1.5, color: '#0d9488' },
            showlegend: showLegend,
        });
    }
    Plotly.newPlot(div, traces, baseLayout('Adults'), { responsive: true });
}

function buildSexRatioChart(div, popData, iterations, showLegend) {
    var sexRows = popData.filter(function(d) {
        return d.category === 'Sex' && d.type === 'all';
    });
    var traces = [];
    for (var i = 0; i < iterations.length; i++) {
        var iter = iterations[i];
        var males = sexRows.filter(function(d) {
            return d.iteration === iter && d.value1 === 'Males';
        }).sort(function(a, b) { return a.generation - b.generation; });
        var females = sexRows.filter(function(d) {
            return d.iteration === iter && d.value1 === 'Females';
        }).sort(function(a, b) { return a.generation - b.generation; });

        var gens = males.map(function(d) { return d.generation; });
        var femaleFrac = gens.map(function(gen, idx) {
            var m = males[idx] ? males[idx].count : 0;
            var f = females[idx] ? females[idx].count : 0;
            return (m + f) > 0 ? f / (m + f) : 0.5;
        });

        traces.push({
            x: gens,
            y: femaleFrac,
            name: 'Iter ' + iter,
            type: 'scatter',
            mode: 'lines',
            line: { dash: iterDash(i), width: 1.5, color: '#8b5cf6' },
            showlegend: showLegend,
        });
    }

    // Reference line at 0.5
    if (sexRows.length > 0) {
        var allGens = unique(sexRows.map(function(d) { return d.generation; }));
        traces.push({
            x: [allGens[0], allGens[allGens.length - 1]],
            y: [0.5, 0.5],
            type: 'scatter', mode: 'lines',
            line: { color: '#ccc', width: 1, dash: 'dot' },
            showlegend: false, hoverinfo: 'skip',
        });
    }

    Plotly.newPlot(div, traces, baseLayout('Female Fraction', { yaxis: { title: { text: 'Female Frac.', font: { size: 11 } }, range: [0, 1.05], tickfont: { size: 10 } } }), { responsive: true });
}

// ---- Grid assembly ----

function buildCharts(data) {
    var allData = data.filter(function(d) { return d.type === 'all'; });
    var environs = unique(allData.map(function(d) { return d.environ; }));
    var container = document.getElementById('charts');
    container.innerHTML = '';

    // Iteration dash legend
    var allIters = unique(data.map(function(d) { return d.iteration; }));
    if (allIters.length > 1) {
        var iterLeg = document.createElement('div');
        iterLeg.className = 'iter-legend';
        iterLeg.innerHTML = 'Iterations: ' + allIters.map(function(it, idx) {
            var style = ITER_DASHES[idx % ITER_DASHES.length];
            var label = { solid: '———', dash: '– – –', dot: '·····', dashdot: '–·–', longdash: '— —' };
            return '<span>' + (label[style] || style) + ' Iter ' + it + '</span>';
        }).join('');
        container.appendChild(iterLeg);
    }

    for (var e = 0; e < environs.length; e++) {
        var env = environs[e];
        var envData = allData.filter(function(d) { return d.environ === env; });
        buildEnvironCharts(container, env, envData);
    }
}

function buildEnvironCharts(container, envName, envData) {
    var populations = unique(envData.map(function(d) { return d.population; }));
    var iterations  = unique(envData.map(function(d) { return d.iteration; }));
    var genes = unique(envData.filter(isGenotypeRow).map(function(d) { return d.category; }));

    var numCols = populations.length;
    var colParts = ['50px'];
    for (var c = 0; c < numCols; c++) {
        if (c > 0) colParts.push('24px');
        colParts.push('1fr');
    }
    var grid = document.createElement('div');
    grid.className = 'chart-grid';
    grid.style.gridTemplateColumns = colParts.join(' ');
    container.appendChild(grid);

    // Column headers with migration arrows
    var ARROW_OPACITY = [1.0, 0.7, 0.45, 0.25];
    grid.appendChild(makeDiv('', 'col-header'));
    for (var p = 0; p < populations.length; p++) {
        if (p > 0) {
            var arrow = document.createElement('div');
            arrow.className = 'migration-arrow';
            arrow.innerHTML = '<svg width="20" height="24" viewBox="0 0 20 24"><polygon points="10,22 3,14 7,14 7,2 13,2 13,14 17,14" fill="#0f3460" opacity="' + ARROW_OPACITY[p - 1] + '"/></svg>';
            grid.appendChild(arrow);
        }
        grid.appendChild(makeDiv('Population ' + (populations[p] + 1), 'col-header'));
    }

    // Allele frequency rows (one per gene)
    for (var g = 0; g < genes.length; g++) {
        (function(gene) {
            addRow(grid, gene, populations, envData, function(div, popData, isFirst) {
                buildAlleleChart(div, popData, iterations, gene, isFirst);
            });
        })(genes[g]);
    }

    // Adults row
    addRow(grid, 'Adults', populations, envData, function(div, popData, isFirst) {
        buildAdultsChart(div, popData, iterations, isFirst);
    });

    // Sex ratio row
    addRow(grid, 'Sex Ratio', populations, envData, function(div, popData, isFirst) {
        buildSexRatioChart(div, popData, iterations, isFirst);
    });

    // Eggs row
    addRow(grid, 'Eggs', populations, envData, function(div, popData, isFirst) {
        buildEggChart(div, popData, iterations, isFirst);
    });
}

function addRow(grid, label, populations, envData, chartFn) {
    grid.appendChild(makeDiv(label, 'row-label'));
    for (var p = 0; p < populations.length; p++) {
        if (p > 0) grid.appendChild(makeDiv('', 'arrow-spacer'));
        var pop = populations[p];
        var div = document.createElement('div');
        div.className = 'chart-cell';
        grid.appendChild(div);
        var popData = envData.filter(function(d) { return d.population === pop; });
        chartFn(div, popData, p === 0);
    }
}

function makeDiv(text, cls) {
    var d = document.createElement('div');
    if (cls) d.className = cls;
    if (text) d.textContent = text;
    return d;
}
