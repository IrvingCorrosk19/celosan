(function () {
    const cfg = window.chCargaHoraria || {};
    const $program = $('#chProgram');
    const $grade = $('#chGrade');
    const $gradeWrap = $('#chGradeWrap');
    const $report = $('#chReport');
    let lastProgramTypeGrades = [];

    function formatHour(value) {
        if (value === null || value === undefined || value === '')
            return '<span class="ch-empty">—</span>';
        const n = Number(value);
        return Number.isInteger(n) ? String(n) : n.toFixed(2);
    }

    function formatHourPlain(value) {
        if (value === null || value === undefined || value === '')
            return '';
        return String(value);
    }

    function viewMode() {
        return $('input[name="chViewMode"]:checked').val() || 'FullProgram';
    }

    function escapeHtml(text) {
        return String(text == null ? '' : text)
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;');
    }

    function hourCell(value, subjectId, block, canEdit) {
        if (!canEdit || !subjectId)
            return formatHour(value);
        return '<input type="number" min="0" step="0.5" class="form-control form-control-sm ch-hour-input"'
            + ' data-subject-id="' + subjectId + '" data-block="' + block + '"'
            + ' value="' + formatHourPlain(value) + '"'
            + ' placeholder="—">';
    }

    function renderReport(data) {
        if (!data || !data.areas || !data.areas.length) {
            $report.html('<p class="text-muted mb-0">No hay asignaturas curriculares para este programa.</p>');
            return;
        }

        const grades = data.grades || [];
        const canEdit = !!(cfg.canEdit && data.canEdit);
        const byGrade = data.viewMode === 'ByGrade';
        const colSpan = byGrade ? 5 : (2 + grades.length * 2 + 1);
        let html = '';
        html += '<div class="ch-title"><h4 class="mb-0">CARGA HORARIA</h4></div>';
        html += '<div class="ch-subtitle"><strong>' + escapeHtml(data.headerTitle) + '</strong></div>';
        html += '<div class="table-responsive"><table class="ch-matrix"><thead>';

        if (byGrade) {
            html += '<tr><th>Área</th><th>Asignatura</th><th>B1</th><th>B2</th><th>Total</th></tr>';
        } else {
            html += '<tr><th rowspan="2">Área</th><th rowspan="2">Asignatura</th>';
            grades.forEach(function (g) {
                html += '<th colspan="2">' + g + '°</th>';
            });
            html += '<th rowspan="2">TOTAL HORAS</th></tr><tr>';
            grades.forEach(function () {
                html += '<th>B1</th><th>B2</th>';
            });
            html += '</tr>';
        }
        html += '</thead><tbody>';

        data.areas.forEach(function (area) {
            html += '<tr><td class="ch-area" colspan="' + colSpan + '">' + escapeHtml(area.name) + '</td></tr>';
            (area.subjects || []).forEach(function (subject) {
                const review = subject.needsReview
                    ? ' <span class="badge badge-warning" title="Materia no reconocida en la hoja MEDUCA">Revisar</span>'
                    : '';
                html += '<tr><td></td><td class="ch-subject">' + escapeHtml(subject.subject) + review + '</td>';
                (subject.gradeLoads || []).forEach(function (load) {
                    const idB1 = load.b1CurriculumLoadSubjectId || load.curriculumLoadSubjectId || '';
                    const idB2 = load.b2CurriculumLoadSubjectId || load.curriculumLoadSubjectId || '';
                    html += '<td>' + hourCell(load.b1, idB1, 'B1', canEdit) + '</td>';
                    html += '<td>' + hourCell(load.b2, idB2, 'B2', canEdit) + '</td>';
                    if (byGrade)
                        html += '<td>' + formatHour(load.total) + '</td>';
                });
                if (!byGrade)
                    html += '<td>' + formatHour(subject.totalHours) + '</td>';
                html += '</tr>';
            });

            html += '<tr class="ch-subtotal"><td colspan="2">SUB TOTAL</td>';
            (area.totalsByGrade || []).forEach(function (t) {
                html += '<td>' + formatHour(t.b1) + '</td><td>' + formatHour(t.b2) + '</td>';
                if (byGrade)
                    html += '<td>' + formatHour(t.total) + '</td>';
            });
            if (!byGrade)
                html += '<td>' + formatHour(area.totalHours) + '</td>';
            html += '</tr>';
        });

        html += '<tr class="ch-grand"><td colspan="2">TOTAL DE HORAS POR NIVEL</td>';
        (data.totalHoursByGrade || []).forEach(function (t) {
            if (byGrade) {
                html += '<td colspan="2"></td><td>' + formatHour(t.total) + '</td>';
            } else {
                html += '<td colspan="2">' + formatHour(t.total) + '</td>';
            }
        });
        if (!byGrade)
            html += '<td>' + formatHour(data.totalHours) + '</td>';
        html += '</tr>';

        html += '<tr class="ch-grand"><td colspan="2">TOTAL DE HORAS</td>';
        html += '<td colspan="' + (colSpanForTotals(byGrade, grades.length)) + '">' + formatHour(data.totalHours) + '</td></tr>';
        html += '<tr class="ch-grand"><td colspan="2">TOTAL DE ASIGNATURAS</td>';
        (data.subjectCountsByGrade || []).forEach(function (c) {
            if (byGrade) {
                html += '<td>' + (c.b1 || 0) + '</td><td>' + (c.b2 || 0) + '</td><td>' + ((c.b1 || 0) + (c.b2 || 0)) + '</td>';
            } else {
                html += '<td>' + (c.b1 || 0) + '</td><td>' + (c.b2 || 0) + '</td>';
            }
        });
        if (!byGrade)
            html += '<td>' + (data.totalSubjects || 0) + '</td>';
        html += '</tr>';
        html += '</tbody></table></div>';

        if (data.inactiveSubjects && data.inactiveSubjects.length) {
            html += '<p class="small text-muted mt-3 mb-1">CLS inactivas (ocultas en la matriz):</p><ul class="small text-muted">';
            data.inactiveSubjects.forEach(function (row) {
                html += '<li>' + escapeHtml(row.grade) + '° · ' + escapeHtml(row.area) + ' · ' + escapeHtml(row.subject) + '</li>';
            });
            html += '</ul>';
        }

        $report.html(html);
        bindSave();
    }

    function colSpanForTotals(byGrade, gradeCount) {
        return byGrade ? 3 : (gradeCount * 2 + 1);
    }

    function bindSave() {
        if (!cfg.canEdit || !cfg.saveUrl)
            return;

        $report.find('.ch-hour-input').off('change.ch').on('change.ch', function () {
            const subjectId = $(this).data('subject-id');
            const block = $(this).data('block');
            const raw = $(this).val();
            const parsed = raw === '' ? null : Number(raw);
            const payload = {
                curriculumLoadSubjectId: subjectId,
                b1: block === 'B1' ? parsed : null,
                b2: block === 'B2' ? parsed : null,
                onlyBlock: block
            };

            $.ajax({
                url: cfg.saveUrl,
                method: 'POST',
                contentType: 'application/json',
                data: JSON.stringify(payload)
            }).done(function (res) {
                if (res && res.success)
                    loadReport();
                else
                    alert((res && res.message) || 'No se pudo guardar.');
            }).fail(function (xhr) {
                const msg = (xhr.responseJSON && xhr.responseJSON.message) || 'No autorizado o error al guardar.';
                alert(msg);
            });
        });
    }

    function fillGradeOptions(programType) {
        lastProgramTypeGrades = programType === 'PREMEDIA' ? [7, 8, 9] : [10, 11, 12];
        const current = $grade.val();
        $grade.empty();
        lastProgramTypeGrades.forEach(function (g) {
            $grade.append($('<option/>').val(g).text(g + '°'));
        });
        if (current && lastProgramTypeGrades.indexOf(Number(current)) >= 0)
            $grade.val(current);
    }

    function toggleGradeWrap() {
        if (viewMode() === 'ByGrade')
            $gradeWrap.show();
        else
            $gradeWrap.hide();
    }

    function loadPrograms() {
        if (!cfg.programsUrl)
            return;
        $.get(cfg.programsUrl).done(function (res) {
            $program.find('option:not(:first)').remove();
            if (!res || !res.success || !res.data || !res.data.length) {
                $report.html('<p class="text-muted mb-0">Aún no hay estructura curricular cargada.</p>');
                return;
            }
            res.data.forEach(function (p) {
                $program.append($('<option/>').val(p.programId).text(p.name).attr('data-type', p.programType));
            });
        });
    }

    function loadReport() {
        const programId = $program.val();
        if (!programId) {
            $report.html('<p class="text-muted mb-0">Seleccione un programa académico para consultar la carga horaria.</p>');
            return;
        }
        const type = $program.find('option:selected').data('type');
        fillGradeOptions(type);
        toggleGradeWrap();

        const params = {
            programId: programId,
            viewMode: viewMode()
        };
        if (viewMode() === 'ByGrade')
            params.selectedGrade = $grade.val();

        $report.html('<p class="text-muted mb-0">Cargando...</p>');
        $.get(cfg.reportUrl, params).done(function (res) {
            if (!res || !res.success) {
                $report.html('<p class="text-muted mb-0">' + escapeHtml((res && res.message) || 'Sin datos.') + '</p>');
                return;
            }
            renderReport(res.data);
        }).fail(function () {
            $report.html('<p class="text-danger mb-0">No se pudo cargar la carga horaria.</p>');
        });
    }

    $program.on('change', loadReport);
    $grade.on('change', loadReport);
    $('input[name="chViewMode"]').on('change', function () {
        toggleGradeWrap();
        loadReport();
    });

    loadPrograms();
})();
