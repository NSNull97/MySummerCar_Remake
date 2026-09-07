"""Read-only reviewed connector-ring selectors on the installed Unity audit CSVs."""
from pathlib import Path
import numpy as np

root = Path('Logs/visual-packet-20260907/graphics')


def points(name):
    return np.loadtxt(root / ('mesh-' + name + '-0.csv'), delimiter=',')


def ring(name, center, normal, radius, tolerance=.00006):
    v = points(name)
    normal = np.array(normal) / np.linalg.norm(normal)
    delta = v - center
    selected = (np.abs(delta @ normal) < tolerance) & (np.linalg.norm(delta, axis=1) < radius)
    indices = np.where(selected)[0]
    _, unique = np.unique(np.round(v[indices], 5), axis=0, return_index=True)
    indices = indices[unique]
    print(name, 'indices=', indices.tolist(), 'center=', v[indices].mean(axis=0).tolist(), 'count=',len(indices))


ring('fuel-strainer', [.07542714,.03248446,1.4858935], [1,0,0], .009)
ring('fuel-pump', [.0710549,.0335229,1.4786583], [1,.010,.01246], .005)
ring('radiator-hose1', [-.203483,.138251,1.344184], [.075,-.438,-.896], .02)
ring('cylinder-head', [-.21082785,.1480776,1.3570999], [-.149,.368,.918], .013)
ring('radiator-hose3', [-.2445036,-.0735197,1.0616207], [.66,.74,.12], .016, .00012)
ring('radiator-hose2', [-.24681855,-.0704072,1.0466983], [-.65,-.74,-.15], .016, .00012)
