window.CelosanEvaluation = (function () {
    const state = {
        academicYear: null,
        scheme: 'Legacy',
        calculationScheme: 'Legacy',
        types: [],
        importedByStudent: {}
    };

    function officialRound(value) {
        if (value == null || isNaN(value)) return null;
        const sign = value < 0 ? -1 : 1;
        return sign * Math.round(Math.abs(value) * 10 + Number.EPSILON) / 10;
    }

    function normalizeType(type) {
        return (type || '').toString().trim().toLowerCase();
    }

    function isWeightedType(type) {
        const n = normalizeType(type);
        return n === 'unidireccional' || n === 'autoevaluación' || n === 'coevaluación';
    }

    function applyContext(ctx) {
        if (!ctx) return;
        state.academicYear = ctx.academicYear || ctx.AcademicYear || null;
        state.scheme = ctx.scheme || ctx.Scheme || 'Legacy';
        state.calculationScheme = ctx.calculationScheme || ctx.CalculationScheme || state.scheme;
        state.types = ctx.types || ctx.Types || [];
    }

    function fillTypeSelect($select) {
        if (!$select || !$select.length) return;
        const previous = $select.val();
        $select.empty();
        state.types.forEach(function (opt) {
            const value = opt.value || opt.Value;
            const label = opt.label || opt.Label || value;
            $select.append($('<option>').val(value).text(label));
        });
        if (previous && $select.find('option').filter(function () { return this.value === previous; }).length) {
            $select.val(previous);
        } else if (state.types.length) {
            $select.prop('selectedIndex', 0);
        }
        $select.trigger('change');
    }

    function headerTypeOrder(activities) {
        const existing = Object.keys(activities || {}).filter(function (k) {
            return activities[k] && activities[k].length > 0;
        });
        const schemeOrder = state.types.map(function (t) { return normalizeType(t.value || t.Value); });
        const ordered = [];
        schemeOrder.forEach(function (t) {
            if (existing.indexOf(t) >= 0 && ordered.indexOf(t) < 0) ordered.push(t);
        });
        existing.forEach(function (t) {
            if (ordered.indexOf(t) < 0) ordered.push(t);
        });
        return ordered;
    }

    function shortLabel(type) {
        const t = normalizeType(type);
        const map = {
            'notas de apreciación': 'Aprec.',
            'ejercicios diarios': 'Ejerc.',
            'examen final': 'Examen',
            'recuperación': 'Recup.',
            'unidireccional': 'Unidir. 80%',
            'autoevaluación': 'Auto. 10%',
            'coevaluación': 'Coeval. 10%'
        };
        return map[t] || (type || '').toString();
    }

    function isHistoricalType(type) {
        if (state.calculationScheme !== 'Weighted801010') return false;
        return !isWeightedType(type);
    }

    function categoryAverage(values) {
        const valid = values.filter(function (v) { return v != null && !isNaN(v); });
        if (!valid.length) return null;
        return valid.reduce(function (a, b) { return a + b; }, 0) / valid.length;
    }

    function computeLegacyFinal(typeAvgs) {
        const parts = [];
        ['notas de apreciación', 'ejercicios diarios', 'examen final'].forEach(function (t) {
            if (typeAvgs[t] != null) parts.push(typeAvgs[t]);
        });
        if (!parts.length) return null;
        return parts.reduce(function (a, b) { return a + b; }, 0) / parts.length;
    }

    function computeWeightedFinal(typeAvgs) {
        const u = typeAvgs['unidireccional'];
        const a = typeAvgs['autoevaluación'];
        const c = typeAvgs['coevaluación'];
        let sum = 0;
        let weight = 0;
        if (u != null) { sum += u * 80; weight += 80; }
        if (a != null) { sum += a * 10; weight += 10; }
        if (c != null) { sum += c * 10; weight += 10; }
        if (weight === 0) return null;
        return officialRound(sum / weight);
    }

    function computeFinal(typeAvgs) {
        if (state.calculationScheme === 'Weighted801010')
            return computeWeightedFinal(typeAvgs);
        return computeLegacyFinal(typeAvgs);
    }

    function formatGrade(value) {
        if (value == null || isNaN(value)) return '';
        return Number(value).toFixed(1);
    }

    function importedScore(studentId) {
        if (!studentId) return null;
        const key = String(studentId);
        if (state.importedByStudent[key] != null) return state.importedByStudent[key];
        if (state.importedByStudent[studentId] != null) return state.importedByStudent[studentId];
        return null;
    }

    return {
        state: state,
        applyContext: applyContext,
        fillTypeSelect: fillTypeSelect,
        headerTypeOrder: headerTypeOrder,
        shortLabel: shortLabel,
        isHistoricalType: isHistoricalType,
        isWeightedType: isWeightedType,
        categoryAverage: categoryAverage,
        computeFinal: computeFinal,
        officialRound: officialRound,
        formatGrade: formatGrade,
        importedScore: importedScore,
        normalizeType: normalizeType
    };
})();
