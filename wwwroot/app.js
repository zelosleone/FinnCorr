document.getElementById('uploadForm').addEventListener('submit', async (e) => {
    e.preventDefault();

    const formData = new FormData(e.target);

    // Collect field definitions
    const file1Fields = collectFields('file1');
    const file2Fields = collectFields('file2');

    if (file1Fields.length === 0 || file2Fields.length === 0) {
        alert('Please define at least one field for each file.');
        return;
    }

    formData.append('File1Fields', JSON.stringify(file1Fields));
    formData.append('File2Fields', JSON.stringify(file2Fields));

    try {
        const response = await fetch('/api/upload', {
            method: 'POST',
            body: formData
        });

        if (!response.ok) {
            const error = await response.text();
            alert(`Error: ${error}`);
            return;
        }

        const result = await response.json();
        displayResults(result);
    } catch (error) {
        alert('An error occurred while uploading the files.');
    }
});

function addField(filePrefix) {
    const container = document.getElementById(`${filePrefix}Fields`);
    const fieldIndex = container.children.length;

    const fieldDiv = document.createElement('div');
    fieldDiv.innerHTML = `
        <label>Field Name:</label>
        <input type="text" name="${filePrefix}Fields[${fieldIndex}].FieldName" required>
        <label>Data Type:</label>
        <select name="${filePrefix}Fields[${fieldIndex}].DataType" required>
            <option value="string">String</option>
            <option value="int">Integer</option>
            <option value="float">Float</option>
            <option value="date">Date</option>
        </select>
        <button type="button" onclick="removeField(this)">Remove</button><br><br>
    `;
    container.appendChild(fieldDiv);
}

function removeField(button) {
    const fieldDiv = button.parentElement;
    fieldDiv.remove();
    updateFieldNames();
}

function updateFieldNames() {
    ['file1', 'file2'].forEach(filePrefix => {
        const container = document.getElementById(`${filePrefix}Fields`);
        Array.from(container.children).forEach((child, index) => {
            const fieldNameInput = child.querySelector(`input[name^="${filePrefix}Fields["]`);
            const dataTypeSelect = child.querySelector(`select[name^="${filePrefix}Fields["]`);
            if (fieldNameInput && dataTypeSelect) {
                fieldNameInput.name = `${filePrefix}Fields[${index}].FieldName`;
                dataTypeSelect.name = `${filePrefix}Fields[${index}].DataType`;
            }
        });
    });
}

function collectFields(filePrefix) {
    const fields = [];
    const container = document.getElementById(`${filePrefix}Fields`);
    Array.from(container.children).forEach(child => {
        const fieldNameInput = child.querySelector(`input[name^="${filePrefix}Fields["]`);
        const dataTypeSelect = child.querySelector(`select[name^="${filePrefix}Fields["]`);
        if (fieldNameInput && dataTypeSelect) {
            const fieldName = fieldNameInput.value.trim();
            const dataType = dataTypeSelect.value;
            if (fieldName) {
                fields.push({ FieldName: fieldName, DataType: dataType });
            }
        }
    });
    return fields;
}

function displayResults(result) {
    const resultsDiv = document.getElementById('results');
    resultsDiv.innerHTML = `
        <h2>Insights</h2>
        <pre>${result.insights}</pre>
        <h2>Graph</h2>
        <img src="${result.graphUrl}" alt="Correlation Graph">
    `;
}