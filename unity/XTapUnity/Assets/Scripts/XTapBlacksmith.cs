using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class XTapBlacksmith : MonoBehaviour
{
    // 11.40: runtime fallback copies for Android builds where dynamically-added
    // Resources .bytes files are present in source but fail to resolve after install.
    static readonly string EmbeddedBlacksmithAtlasPngBase64 = "iVBORw0KGgoAAAANSUhEUgAAAQAAAAIACAIAAADi+CWsAAAABmJLR0QA/wD/AP+gvaeTAAAbWklEQVR4nO3dbZAU1bnA8WdmX2CXnVkEbpUiiFuiwgoB3DWEqguWJgrJ1aRy1VAmRVKVIMRPV0wl8epNLK8xt1IphUo+YNCYKFcTEo0mppI1CRiTVCzcXQEB43XFN16kIjPuDAq7CPT90OxMb79Nz0vPTM/z/32gzvaeYc8+fZ49fbpPd8faO6YKoFWziKxf01vrZiCQdZsGhP1VOes2DcRr3Qaglppzpf/51X6fekvndjY3xZ7dMxx+k+DiP/99pm3Ld59426c++8vf7dedZxbyCWB41146t/Nf5ybNOsS0Tvjsr2VzO5d2d5rlbewvX/kEEMMQkURbPNEWs9ZYeH5yYVfCLC+5KJGYGBvYl6liC1U7etw4evy0+/fG9leyfdxx7Lj9dXEy0RZnf+Vkj522xbPZViPRFku2x7LHzvx9sUbTNO+8DhEhplWQbI+JyNHj7t81xBCRRHss0RbLju3URW77yxCD/SUiyba4IbHs8XFjpz0BRCR7zDiUPmWWZ/3LKWeF9z44lauAMDX5fdM482/22OmDqZPmtvOmuu2v908dPHKy4o2LnqnNIvZjR5cEsHr+/7IicsW8ybktf/1H5q8v8+ek9gzDcJb//sqwiPHxj0zJfeu5ve/9ZS/TgDxr3MQ/AXIzZaulczuXzu30/xn+Jyj0cA1gEEECaDj+/fbnupzVLr/krMsvOcv/v/rvX7xRXPtqxPUXDML8Ba2xyrGeBTJcyyUo8+MItC/Mv2TWf0v/eY2+v7xjZT0LJK7lex5/K8SWNbQSQnfH9bNEPPeFjNvsniR3bdlX7A+NihJ+tTtXXiC+f1AKzAFQt878ObOUxVGGyStWQgJEmXMWII4yTF6xIgEiy7DsTUYAf16xEuck2PqvdTuqyWtf2GsVLsPkGR9GgKhiDhBcsDkAh5T1w/OQ1Vol/21GbH9esRJGgAhjCAjOewggAaKKATs4nwHV/X4As3z3L98Ms1Vw4Yy55xTYcmrDLN/xv6+E17CIGh+TfKxymm3fdiujtjwnAfZ/4c8tVoWXQqDGfCfBhqUMf66xYg4QWT5Xd+DOfzEcIoUBuzQFJsEcUtYP/31hTubGVviyxwpzjZVzEkwK1A/2Reh4MBZUC7AUArXCvggfk+Cocl4IQyG+F8I4q1Cf2BehYg4A1VgKUf/YFyFiBIBqrAWqe577gh1WGt9JMGfe6gf7ogo4DRpVFXwwnBKusWIpRD1jX4SOSTBUIwGgmmUSzP0V9abAvuCQtVgusWIEgGpcCa5/7IsQcRo0qngWVmlsseIQCKpxJbh+sS+qgLVAdY+1QBXGWSBgDEsh6hn7InSMAFCNe4LrHfsiVDwWpY757ouxJ8PxVIigXGPFIRBUYylE/WNfhIgRAKpxJbh+sS+qgBEAqrEUou6xFKLCeENMQ2A5dGl4KkSEsC9Cx5Xgese+CBWTYKjGHCC6OGQtlkusWAtUx9gX4WMpRP0Lui/uXX2piHztwRfDbEzEFIwJV4Lrl/++8Ho4LqdEnXweJMwkGKqRAFCNs0DR5TVn4xjIyTM+1rNAPB233rAvQseV4HrHvggVcwCoxhwgqrxOfXLE5OQTHxKg0axf01PsR9ZtGgyjJRVXwq9WEAkQXRW8dNnwo4ZnrKyvSDKfmpIvo7aK3RfrNg2E2Jo6UPAXXNEzfXnP9NyX23Ydfnr7Af+PMAlGg7D1fhG5csHZ1y6e4f8pDoEib0pywoUzkrVuRe0lJ7U6NybaW3LBmZKckM6O2iqQANGWcuxRtXa8nhaRJXOm5bb0D6X6h1K5L9PZUWe4SICoMucGqcxIKjNS67bUi1f3Z45kRszDnr7Bg30Dhwp+hARAQ9m6852WpriI9A0W7v1CAkTXhrWX1boJdW1F7/TClTgLBOUYAaKn4c/3VxMjAFRjBIie9Wt6a92EBrFu0wCPRqxn7IvQMQJE1XefeNvnu0vndjY3xZ7dM1y19kTL7dedZxZIgAa0dG7n0u5Os0wO+LOuBhXXMmrLa18YYohIoi2ebBt3JmPh+cmFXQmzvOTiZKItPrAvE3IbIyN7/PTR46etWzgLFFmGiCHJtniiLZbbZu39pnnndfRe0Fn1xtWjRFss2RY345bDIVBU5SbI2ePGwdRJc+PMaSedNdPvnzyQctmuzblTm8VxSoEEiCy35/09/0pGDPn4R87K1frLy8N/2cs0QETcn41IAjSO/7rhfOfGZd2Tl3VP9v/gd375ZhjtqTjXXzAIn1+QBIgqwzKYG+VdKyjz4/XPJ1YkQGR5PMns7l+8UfWmVEkJv9q3Ptcl4vfUNxIgqsy/ZNZ/rdth5RUrIQEizGvlCv3fyXuVDwkQVTwbOjif+JAA0UUKBBfk8eiIlDMntS1lcZRh8oqVkABRxiQguACPRkS0eL3PhBHAyefdLyRAdDEHCC7QHIAg1if3feEc1L/9s6HqNChCrDFxvb+OESCy+HtVGg6BGgX3cBeLSXAD4aWeRWMS3Fg4BioNI0BD4ACoWEyCGwvHQEXzvSPM7Q471JL/vuAAqDSMAI2CY6BisRy6kYzd3pEvw59rrHguEFRjBIgqwzIJ5r3OwbjEihEAqjECRJX5l6zYt8lr5horRgCoRgJANRIAqpEAUI13hNUz/33B/ioW9wM0EG6EL40tVhwCQTUSAKqRAFCNBIBqJABUIwGgGqdBo4ubIkvDYjhgDAkA1exPhXCWUVueT4XgKR5Fco0VIwBUIwGgGgkA1TgNGl0shy6WS6wYAaAar0iqf+yLEDECQDUSAKoxCY4qLlyWhgthQB4JANU4BIouztqVhusAwBgSAKqRAFCNBIBqJABU4yxQVHFHWLFcY0UCRBfLoYvFcmhgPBIAqtmfCsExZf1gX1QBIwBU4xVJ9Yx9ETpGAKhGAkA1rgNEF8uhS8NbIhsCt0SWhlsigTwSAKqRAFCNBIBqJABUIwGgGqdBo4ulK8XifgBgPEaAqGL5erF4SR5gRwJANRIAqpEAUI13hNU/r33B/ioNp0Eb1H039dx3U0+tW1FfCsbE/lQIZxm1xb4IFSMAVCMBoBpXgqPK65CVQyYnn/gwAkA1EgCqcQgUXV7LoTkGcvJcOs4IANUYARrN+jW9xX5k3aaBMFpScSX8agUxAkA1RoDGEZU/5CUr+Auu6Jm+vGd67sttuw4/vf2A/0cYAdAgbL1fRK5ccPa1i2f4f4oRIKpyN/hN7Zww+9xkrZtTe8lJrc6NHe0tueBMSU5IZUZ5S2TDMEQknR2pdTPqxYv70iKyZM603JYXhlL9r6ZyX6Yyo+nsCE+Hbiip7GgqO1rrVtSLoQOZI5kR87Cnb/Bg38Chgh/hFUn1jH1RtK0732lpiotI32Dh3i+MANG1Ye1ltW5CXVvRO71wJc4CQTlGgOhp+PP91UQCRIzBev+K4hAIqpEAUM3+VAgetlo/2BdVwAgA1UgAqEYCQDUSAKqRAFCNBIBqJABUIwGgGgkA1UgAqMYrkuof+yJEjABQjQSAaiQAVCMBoBoJANVIAKhGAkA1EgCqkQBQjQSAavanQjjLqC32RagYAaAaCQDVSACoRgJANRIAqpEAUI13hNUz9kXoGAGgGgkA1UgAqEYCQDUSAKqRAFCNBIBqfgmwfk3v+jW9VWsKhJhXHSMAVCMBoBoJANVIAKhGAkA1EgCq2Z8KYf3Xuh3V5LUvUHGMAFCt8CuSNqwt+rrMLT/qL6NJjWPD2stK/Sivq6oSRgCo1uzzPf6Ql6lgAFf0nLuid3ruy60733l6+4GQG4VxGAFqxtb7ReTjC8+5dvGMWrVHJ5cRYGpnq0hn9ZuiTWdHq3NjYlLrhTPOBH9qZ2sqc6K6jVLHngDp7EhN2qHQi/vSIrJkzrTclheGUv2vpnJfpjIn2B1hsydAKjuayo7WpCkKDR3IHMmMmIc9fYMH+wYO1bpF6vhNglEFW3e+09IUF5G+QXp/DeQToIyT1qgA24QY1cFZIKgWa++YWus2oAgGa4MqijlAxMRisVo3oaE0iwh3YdfKuk0DIvLmH2+udUOUOv+qjcwBoFr+EOiex9/yqbese3JzU2zb7vfCb5IKd1w/y7ald+XjPvXX3NA9oaXph4/tDrNRigxsud4sWG+I8ZxdLeuevLTbvD5vbH2JHAiFT/zX3NC95vpus9YPHiUHKsmeAMn2eLK9yVpjUVdyYVfSLC+5uLNjYnxgX6aaTWwk2WOnssdOu37LK/43XjPnxn+ba5a/9Jk5HRPjDz+1N+x2Nipn/PMJcNowRCTRFk+0x7IfnKm0qKsz1/tN82clRKT/teHQG9twkpPihhEf/uCU63dd43/jNXNzvd903fKLROSnT+4JubENyDX+1hHgtIgYYmQ+OLX/3TOLEGdO+9D5H6WOfvj2u6wXKtpMaZWxODu5xv+fwy6rQd8dPkH8S+Aaf8ccwLCURf72cloM46qF+RWLz+5J/Xl3OvTGNqTxsbV/0y3+D/1qrxjGLasW5qpt3LJ74xamASVxi7/9EMgQI1e++/MXOf+TK+ZNvWJegYvH33rs1XLbWpdcAxKEGRBrbJ2c8d/z5Bec1W5eOf/mlfP9f9y8zz5aWjvrnGtAgjAD4hp/+whg+P6VCojL9TZBYkv8w+MTW0sCnM4/huZMWURE7tj8SjXaGAUlhOKeVXPEO7ZWXnW6P725hKY2pBJC8fJvVolv/F0mwbmydTvK4RXbIHWIf/l84u+YAxj5snU7yuEV2yB1iH/5fOLPWiCo5lwKYVjKYiujNF6xDVKH+JfPJ/6MAFCNBIBqhR+Oy8NZKyFIPIl/eDzjyfsBqiHI8/6Jf3h84u93T/A3fsKak3KVE8MLP/VQBVuiU8EYOg+BrP+isvxjS/zD5hJb+yGQs4zK8j8E8q+D8vkcAtknCt//8gIR+fpDu6rQrEblFsOgk+DXfr9aRGZ/8sGwGqeAWwy5DgCMIQGgmvN+AC7FV55XbIPUIf7l84k/IwBUIwGgmvuDsRiCKytIPIl/eHziyQgA1YJcCeYvUPmCXOUl/uHxjH/hxXD3rr602J926wMvFt3CKLjvpqJDYSpnMdzrz6wt9sd1Xf2jYj8SCW/8oehQmHzizyEQVPNbCnHrA4NVbk2dKxiQ5ZdOX95zTu7LbbsO//aFg45aQZdCdF19f9FNbGgFA/Ifq3pvWZV/28v9W3Z878fbHbWYBIfD1vtF5MoFZ1/z0XNr1R5tbL1fRL66ctE3v7LY/1Muq0GnJFtnS9K9OrwlJ7U4NybaW2afeyaYU5Kt6eyJgqtBiX9pOia63NwyaUKzf/zth0Cp7AinHUqz4/W0iCyZk3+QcP9Qqn8olfsynR1NZUf9D4GIf8m2/G6viKy+If8g4c1P7X7k1y/lvnSNvz1p0tnRdJZHb5do6ED2SGbk2sUzRKRv8NAzxb/8nfiX4zubnj+SGblt9cdEZMMjAxs29xf8iPsNMSjZ1p2HW5riItJXZO8n/hWx8ec7JrQ2iciGRwaC1M8nwIa1vCy1klb0Ti+q/lt/4mWplbTui5cFqcZZIKgWa+8o8LYLoIExAkC1ZhFZv4aj/9pYt2lARN78I0f/tXH+VRsZAaBa/izQPY+/5VNvWffk5qbYtt28Jr4y7rh+lm1L78rHfeqvuaF7QkvTDx/jWX2VMbDlerPgfkeYzbLuyUu7O81aW18iB0LhE/81N3Svub7brPWDR8mBSrInQLI9nmxvstZY1JXMvSx+ycWdHRPjA/sy1WxiI8keO5U95vWOMPf433jNnNzL4r/0mTkdE+MPP7U37HY2Kmf87e8IS7TFE+2x7AdnKi3q6sz1ftP8WQkR6X9tOPTGNpzkpLhhxIc/OOX6Xdf433jN3FzvN123/CIR+emTe0JubANyjb/LWyIzH5za/+4Jc+PMaR86/6PU0Q/ffpf1KkWbKa0yFmcn1/j/c/iEs+a7wyeIfwlc4++YAxiWssjfXk6LYVy1ML/C8dk9qT/vTofe2IY0Prb2b7rF/6Ff7RXDuGVVfoXjxi27N25hGlASt/g7XpNqeZ383Z+/yPmfXDFv6hXzClw8/tZjr5bb1rrkGpAgzIBYY+vkjP+eJ7/grHbzyvk3r5zv/+PmffbR0tpZ51wDEoQZENf4Ox+NmC+XjEfZ2ASJLfEPj09sLQng8Tr5Oza/Uo02RkEJobhn1Rzxjq2VV53uT28uoakNqYRQvPybVeIbf5dJcK5s3Y5yeMU2SB3iXz6f+DvmAEa+bN2OcnjFNkgd4l8+n/izFgiqOZdC2J+hzoyqfF6xDVKH+JfPJ/6MAFCNBIBqfo9GdCujNEHiSfzD4xnPwk+H5hC0fOU8HZr4l88n/i5Pk8v5xk9Yc1KucmJ44aceqmBLdCoYwyAvyECl+MeW+IfNJbbuT4Zj2A1PwYfj+tRB+Qo8HNda/v6XF4jI1x/aVYVmNSq3GAadBL/2+9UiMvuTD4bVOAXcYsh1AGAMCQDVnPcDcCm+8rxiG6QO8S+fT/wZAaAaCQDV3B+MxRBcWUHiSfzD4xNPRgCoFuRKMH+ByhfkKi/xD49n/Asvhrt39aXF/rRbH3ix6BZGwX03FR0KUzmL4V5/Zm2xP67r6h8V+5FIeOMPRYfC5BN/DoGgmt9SiFsfGKxya+pcwYDYXha/bdfh375w0FEr6FKIrqvvL7qJDa1gQGwvi79/y47v/Xi7oxaT4HDYer+IXLng7Gs+em6t2qONrfeLyFdXLvrmVxb7f8plNeiUZOtsSbpXh7fkpBbnxkR7y+xzzwRzSrI1nT1RcDUo8S9Nx0SXm1smTWj2j7/9ECiVHeG0Q2l2vJ4WkSVz8g8S7h9K9Q+lcl+ms6Op7Kj/IRDxL9mW3+0VkdU35B8kvPmp3Y/8+qXcl67xtydNOjuazvLo7RINHcgeyYxcu3iGiPQNHnqmyJfFC/Evz3c2PX8kM3Lb6o+JyIZHBjZs7i/4EfcbYlCyrTsPtzTFRaSvyN5P/Cti4893TGhtEpENjwwEqZ9PgA1reVlqJa3onV5U/bf+xMtSK2ndFy8LUo2zQFAt1t5R4G0XQANjBIBqzSKyfg1H/9Bo3aYBRgColj8L9N0n3vapt3RuZ3NT7Nk9vB4YjeD2684zC+53hNks6568tLvTLG/b/V6oLQOqyZ4AyfZ4om3ccdGirmTuZfFLLk52TIwN7MtUs4lApRw9fjp7bNw7wvJ93RDDECPRFk+2x3Ibrb3fNH9WoveCzrAbClRcsj2WaIub/Ty30XFHmEjmmHHgyIfmxpnTTjr/o/T7J/ePVQCiYsa0FvF7NqjbfWN//8ewGPKJBVNytZ7b+95ze5gGIILc7om0jACWccEs37nyAud/cvklZ11+yVn+P+iuLfvKaCbgzrVDBmHtkIb3TfFG7ttlPouGR9mgrvj0bcdyaMcocefPXgu/hUBhJXTFu26cLd59W9xGgHxZHGUgirz6trjNAQxxmw8A0eXVt8XtLJClLI4yEEVefTvIJJhDIERdsEmw9QMeZSCKfPqz8w0x+bJ1OxBdXn1b3FaDcgiERuPVt4URABoEGgGcbnt4b5itAkJXsA9zSyRUC/KGGKAxBHhDjLMMNBLeEAPkkQBQze8VSUAj4hVJwJjCr0kFGgOvSQXsuA4APVz6NiMAVCMBoBpXgqELk2Agjwth0IZJMDCGBIBqXAmGFq59mwth0IMLYcB4JABUIwGgGtcBoE3gm+LvXb1IRL724I7qtAuoOGcfZikEkEcCQLUg1wGYDyDqPK9xMQJAtcJLIVgWgajzWebDCADVSACoFuRCGMdAiDrP/uz3ggzTfTf1FPvTbn1gsNiPAAWV0BULCuWmeObNqCs+fdtvBFi3iT/kqCMFO+SKnunLe87Jfblt1+Gntx/0/wg3xKBB2Hq/iFy54GwReXr7gbENvm+IyZmSnHDhjGQYTQTCk5zU6tyYaG/JdeYpyQnp7Kitgj0BUo4aQCTseD0tIkvmTMtt6R9K9Q+lcl+ms6PO7m2fBKcyo6kMOYBIenV/9khm5NrFM0Skb/BQ38Chgh/xug4ARNLWne+0NMVFpG+wwPTXlE+ADWsvC6tRQNWt6J0epBpLIaBarL1jaq3bANQMIwBUaxaR9Wt6a90MoAbWbRpgBIBq+bNA333ibZ96S+d2NjfFnt0zHH6TgNDdft15ZsF6IczzOsCy7slLuzvN8rbd74XaMqCa7AmQbI8n2sYdFy3qSi7sOrOaYsnFyY6JsYF9mWo2EaiUo8dPZ4+dtm7J93VDDEOMRFs82R7LbbT2ftP8WYneCzrDbihQccn2WKItbvbz3EbHUyFEMseMA0c+NDfOnHbS+R+l3z+5f6wCEBUzprWI3w0xbs+O+Ps/hsWQTyyYkqv13N73ntvDNAAR5PZcFMsIYBkXzPKdKy9w/ieXX3LW5Zec5f+D7tqyr4xmAu5cO2QQ1g5peD8d2sh92+eMUBBlfhyoLJ++7bgp3jFK3Pmz18JvIVBYCV3xrhtni3ffFrcRIF8WRxmIIq++LW5zAEPc5gNAdHn1bXE7C2Qpi6MMRJFX3w4yCeYQCFEXbBJs/YBHGYgin/7sGAGMfNm6HYgur74tbqtBOQRCo/Hq28IIAA0CjQBOtz28N8xWAaEr2Ie5JRKq8XRo6OH7dGiufUED3hIJ5JEAUC3IWyKBRjKubzMCQDXHTfFuNw0ADcC1bzMCQDWuA0APl77NCADVSACoxpVg6MIkGMjjQhi0YRIMjCEBoBpXgqGFa9/mQhj04EIYMB4JANVIAKjGdQBoE/im+HtXLxKRrz24ozrtAirO2YdZCgHkkQBQLch1AOYDiDrPa1yMAFCt8FIIlkUg6nyW+TACQDUSAKoFuRDGMRCizrM/+70gw3TfTT3F/rRbHxgs9iNAQSV0xYJCuSmeeTPqik/f9hsB1m3iDznqSMEOuaJn+vKec3Jfbtt1+OntB/0/wg0xaBC23i8iVy44W0Se3n5gbIPvG2JypiQnXDgjGUYTgfAkJ7U6NybaW3KdeUpyQjo7aqtgT4CUowYQCTteT4vIkjnTclv6h1L9Q6ncl+nsqLN72yfBqcxoKkMOIJJe3Z89khm5dvEMEekbPNQ3cKjgR7yuAwCRtHXnOy1NcRHpGyww/TXlE2DD2svCahRQdSt6pwepxlIIqBZr75ha6zYANdNscNkWinEIBNVIAKhGAkA1EgCqkQBQjQSAaiQAVCMBoBoJANVIAKhGAkA1EgCqkQBQjQSAaiQAVCMBoBoJANVIAKhGAkA1EgCqkQBQjQSAaiQAVCMBoBoJANVIAKhGAkA1EgCqkQBQjQSAaiQAVCMBoBoJANVIAKhGAkA1EgCqkQBQjQSAaiQAVCMBoBoJANX+HzQu8gl9RXNjAAAAAElFTkSuQmCC";
    const float UiFontScale = 2.15f;
    enum ForgeMode
    {
        Enhance,
        Synthesis,
        Dismantle
    }

    public bool IsOpen { get; private set; }

    RectTransform host;
    Font font;
    XTapInventory inventory;
    Action onClosed;

    GameObject overlay;
    RectTransform panel;
    RectTransform heldContent;
    RectTransform groundContent;
    Text heldCountText;
    Text groundCountText;
    Text heldPageText;
    Text groundPageText;
    Button heldPrevButton;
    Button heldNextButton;
    Button groundPrevButton;
    Button groundNextButton;
    int heldPage;
    int groundPage;

    Text ruleText;
    Text selectionText;
    Text chanceText;
    Text resultText;
    Text targetSlotText;
    Text materialSlotText;
    RectTransform targetSlot;
    RectTransform materialSlot;

    Texture2D skinAtlas;
    Sprite backgroundSkin;
    Sprite panelSkin;
    Sprite slotSkin;
    Sprite tabNormalSkin;
    Sprite tabSelectedSkin;
    Sprite buttonNeutralSkin;
    Sprite buttonPrimarySkin;
    Sprite statusBarSkin;

    Button enhanceTab;
    Button synthesisTab;
    Button dismantleTab;
    Button executeButton;

    GameObject probabilityOverlay;
    XTapForgeDuelView duelView;

    bool probabilityBusy;
    Coroutine probabilityRoutine;

    ForgeMode mode = ForgeMode.Enhance;
    string targetId;
    readonly List<string> materialIds = new List<string>();

    public void Initialize(RectTransform parent, Font uiFont, XTapInventory bag, Action closed)
    {
        host = parent;
        font = uiFont;
        inventory = bag;
        onClosed = closed;

        XTapUiSkin.EnsureLoaded();
        LoadVisualAssets();
        BuildUi();
        overlay.SetActive(false);
    }

    public void Open()
    {
        if (overlay == null) return;

        IsOpen = true;
        overlay.SetActive(true);
        overlay.transform.SetAsLastSibling();
        ClearSelection();
        heldPage = 0;
        groundPage = 0;
        Refresh();
    }

    public void Close()
    {
        if (probabilityBusy) return;
        IsOpen = false;
        ClearSelection();
        if (overlay != null) overlay.SetActive(false);
        if (onClosed != null) onClosed();
    }

    void BuildUi()
    {
        overlay = new GameObject("BlacksmithOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        overlay.transform.SetParent(host, false);

        Image dim = overlay.GetComponent<Image>();
        if (backgroundSkin != null)
        {
            dim.sprite = backgroundSkin;
            dim.type = Image.Type.Simple;
            dim.preserveAspect = false;
            dim.color = Color.white;
        }
        else
        {
            dim.color = new Color(.012f, .008f, .006f, .992f);
        }
        dim.raycastTarget = true;
        Anchor(dim.rectTransform, 0f, 0f, 1f, 1f);

        panel = new GameObject("ForgePanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<RectTransform>();
        panel.SetParent(overlay.transform, false);
        Anchor(panel, 0f, 0f, 1f, 1f);

        Image body = panel.GetComponent<Image>();
        body.color = new Color(0f, 0f, 0f, .16f);
        body.raycastTarget = true;

        Text title = MakeText(panel, "대장간", 31, TextAnchor.MiddleLeft, true);
        title.color = new Color(1f, .73f, .30f, 1f);
        Anchor(title.rectTransform, .045f, .935f, .62f, .995f);

        Button close = MakeButton(panel, "닫기", 17, new Color(.13f, .075f, .050f, 1f));
        ApplyButtonSkin(close, buttonNeutralSkin);
        Anchor(close.GetComponent<RectTransform>(), .80f, .945f, .955f, .990f);
        close.onClick.AddListener(Close);

        enhanceTab = MakeButton(panel, "강화", 19, new Color(.28f, .12f, .035f, 1f));
        synthesisTab = MakeButton(panel, "합성", 19, new Color(.09f, .07f, .065f, 1f));
        dismantleTab = MakeButton(panel, "분해", 19, new Color(.09f, .07f, .065f, 1f));
        ApplyButtonSkin(enhanceTab, tabNormalSkin);
        ApplyButtonSkin(synthesisTab, tabNormalSkin);
        ApplyButtonSkin(dismantleTab, tabNormalSkin);

        Anchor(enhanceTab.GetComponent<RectTransform>(), .035f, .855f, .325f, .925f);
        Anchor(synthesisTab.GetComponent<RectTransform>(), .355f, .855f, .645f, .925f);
        Anchor(dismantleTab.GetComponent<RectTransform>(), .675f, .855f, .965f, .925f);

        enhanceTab.onClick.AddListener(delegate { SetMode(ForgeMode.Enhance); });
        synthesisTab.onClick.AddListener(delegate { SetMode(ForgeMode.Synthesis); });
        dismantleTab.onClick.AddListener(delegate { SetMode(ForgeMode.Dismantle); });

        Text slotGuide = MakeText(panel, "선택하면 목록이 당겨집니다 · 위 슬롯을 누르면 선택 해제", 13, TextAnchor.MiddleCenter, false);
        slotGuide.color = new Color(.82f, .72f, .60f, 1f);
        Anchor(slotGuide.rectTransform, .05f, .815f, .95f, .850f);

        targetSlot = MakeSlotPanel(panel, "TargetSlot", .055f, .595f, .465f, .805f);
        materialSlot = MakeSlotPanel(panel, "MaterialSlot", .535f, .595f, .945f, .805f);

        Image targetSlotImage = targetSlot.GetComponent<Image>();
        targetSlotImage.raycastTarget = true;
        Button targetSlotButton = targetSlot.gameObject.AddComponent<Button>();
        targetSlotButton.targetGraphic = targetSlotImage;
        targetSlotButton.onClick.AddListener(ClearTargetSelection);

        Image materialSlotImage = materialSlot.GetComponent<Image>();
        materialSlotImage.raycastTarget = true;
        Button materialSlotButton = materialSlot.gameObject.AddComponent<Button>();
        materialSlotButton.targetGraphic = materialSlotImage;
        materialSlotButton.onClick.AddListener(ClearMaterialSelection);

        Text targetLabel = MakeText(targetSlot, "대상", 18, TextAnchor.UpperCenter, true);
        targetLabel.color = new Color(1f, .80f, .42f, 1f);
        Anchor(targetLabel.rectTransform, .04f, .72f, .96f, .98f);

        targetSlotText = MakeText(targetSlot, "+", 18, TextAnchor.MiddleCenter, true);
        targetSlotText.color = new Color(.92f, .87f, .80f, 1f);
        Anchor(targetSlotText.rectTransform, .08f, .10f, .92f, .72f);

        Text materialLabel = MakeText(materialSlot, "제물", 18, TextAnchor.UpperCenter, true);
        materialLabel.color = new Color(1f, .80f, .42f, 1f);
        Anchor(materialLabel.rectTransform, .04f, .72f, .96f, .98f);

        materialSlotText = MakeText(materialSlot, "+", 18, TextAnchor.MiddleCenter, true);
        materialSlotText.color = new Color(.92f, .87f, .80f, 1f);
        Anchor(materialSlotText.rectTransform, .08f, .10f, .92f, .72f);

        RectTransform ruleBar = MakePanel(panel, "RuleBar", new Color(.04f, .03f, .025f, .92f));
        Anchor(ruleBar, .035f, .525f, .965f, .592f);
        ApplyPanelSkin(ruleBar, statusBarSkin);

        ruleText = MakeText(panel, "", 13, TextAnchor.MiddleCenter, true);
        ruleText.color = new Color(1f, .86f, .58f, 1f);
        Anchor(ruleText.rectTransform, .055f, .535f, .945f, .585f);

        heldCountText = MakeText(panel, "", 17, TextAnchor.MiddleLeft, true);
        heldCountText.color = new Color(.98f, .91f, .78f, 1f);
        Anchor(heldCountText.rectTransform, .045f, .445f, .50f, .492f);

        heldPrevButton = MakeButton(panel, "◀", 18, new Color(.09f, .07f, .06f, 1f));
        ApplyButtonSkin(heldPrevButton, buttonNeutralSkin);
        Anchor(heldPrevButton.GetComponent<RectTransform>(), .635f, .445f, .735f, .492f);
        heldPrevButton.onClick.AddListener(delegate { ChangeStoragePage(XTapGearBlockData.LocationHeld, -1); });

        heldPageText = MakeText(panel, "", 17, TextAnchor.MiddleCenter, true);
        heldPageText.color = new Color(1f, .84f, .50f, 1f);
        Anchor(heldPageText.rectTransform, .740f, .445f, .855f, .492f);

        heldNextButton = MakeButton(panel, "▶", 18, new Color(.09f, .07f, .06f, 1f));
        ApplyButtonSkin(heldNextButton, buttonNeutralSkin);
        Anchor(heldNextButton.GetComponent<RectTransform>(), .860f, .445f, .960f, .492f);
        heldNextButton.onClick.AddListener(delegate { ChangeStoragePage(XTapGearBlockData.LocationHeld, 1); });

        RectTransform heldViewport;
        inventory.BuildSharedStorageZone(
            panel,
            "ForgeHeldZone",
            .035f, .335f, .965f, .445f,
            out heldViewport,
            out heldContent
        );

        groundCountText = MakeText(panel, "", 17, TextAnchor.MiddleLeft, true);
        groundCountText.color = new Color(.98f, .91f, .78f, 1f);
        Anchor(groundCountText.rectTransform, .045f, .290f, .50f, .337f);

        groundPrevButton = MakeButton(panel, "◀", 18, new Color(.09f, .07f, .06f, 1f));
        ApplyButtonSkin(groundPrevButton, buttonNeutralSkin);
        Anchor(groundPrevButton.GetComponent<RectTransform>(), .635f, .290f, .735f, .337f);
        groundPrevButton.onClick.AddListener(delegate { ChangeStoragePage(XTapGearBlockData.LocationGround, -1); });

        groundPageText = MakeText(panel, "", 17, TextAnchor.MiddleCenter, true);
        groundPageText.color = new Color(1f, .84f, .50f, 1f);
        Anchor(groundPageText.rectTransform, .740f, .290f, .855f, .337f);

        groundNextButton = MakeButton(panel, "▶", 18, new Color(.09f, .07f, .06f, 1f));
        ApplyButtonSkin(groundNextButton, buttonNeutralSkin);
        Anchor(groundNextButton.GetComponent<RectTransform>(), .860f, .290f, .960f, .337f);
        groundNextButton.onClick.AddListener(delegate { ChangeStoragePage(XTapGearBlockData.LocationGround, 1); });

        RectTransform groundViewport;
        inventory.BuildSharedStorageZone(
            panel,
            "ForgeGroundZone",
            .035f, .180f, .965f, .290f,
            out groundViewport,
            out groundContent
        );

        RectTransform statusBar = MakePanel(panel, "StatusBar", new Color(.04f, .03f, .025f, .92f));
        Anchor(statusBar, .035f, .080f, .965f, .162f);
        ApplyPanelSkin(statusBar, statusBarSkin);

        selectionText = MakeText(panel, "", 12, TextAnchor.MiddleLeft, true);
        selectionText.color = new Color(.92f, .84f, .72f, 1f);
        Anchor(selectionText.rectTransform, .045f, .120f, .70f, .160f);

        chanceText = MakeText(panel, "", 14, TextAnchor.MiddleRight, true);
        chanceText.color = new Color(1f, .66f, .20f, 1f);
        Anchor(chanceText.rectTransform, .70f, .120f, .955f, .160f);

        resultText = MakeText(panel, "블록을 선택하세요.", 12, TextAnchor.MiddleCenter, false);
        resultText.color = new Color(.88f, .82f, .74f, 1f);
        Anchor(resultText.rectTransform, .045f, .085f, .955f, .120f);

        Button clear = MakeButton(panel, "초기화", 16, new Color(.095f, .075f, .065f, 1f));
        ApplyButtonSkin(clear, buttonNeutralSkin);
        Anchor(clear.GetComponent<RectTransform>(), .045f, .020f, .405f, .078f);
        clear.onClick.AddListener(delegate
        {
            ClearSelection();
            resultText.text = "선택을 초기화했습니다.";
            Refresh();
        });

        executeButton = MakeButton(panel, "작업", 18, new Color(.38f, .16f, .045f, 1f));
        ApplyButtonSkin(executeButton, buttonPrimarySkin);
        Anchor(executeButton.GetComponent<RectTransform>(), .595f, .020f, .955f, .078f);
        executeButton.onClick.AddListener(Execute);

        BuildProbabilityMachineUi();
        SetMode(ForgeMode.Enhance);
    }

    void BuildProbabilityMachineUi()
    {
        probabilityOverlay = new GameObject("ForgeDuelOverlay", typeof(RectTransform));
        probabilityOverlay.transform.SetParent(overlay.transform, false);
        duelView = probabilityOverlay.AddComponent<XTapForgeDuelView>();
        duelView.Initialize(font);
        probabilityOverlay.SetActive(false);
    }

    RectTransform MakeSlotPanel(Transform parent, string name, float x1, float y1, float x2, float y2)
    {
        RectTransform slot = MakePanel(parent, name, new Color(.055f, .050f, .050f, 1f));
        Anchor(slot, x1, y1, x2, y2);
        ApplyPanelSkin(slot, slotSkin);
        if (slotSkin == null)
            Frame(slot, new Color(.38f, .27f, .17f, 1f), 2f);
        return slot;
    }

    void SetMode(ForgeMode next)
    {
        if (probabilityBusy) return;
        mode = next;
        ClearSelection();
        if (resultText != null) resultText.text = "블록을 선택하세요.";
        Refresh();
    }

    void ClearSelection()
    {
        targetId = null;
        materialIds.Clear();
    }

    void Refresh()
    {
        if (panel == null || inventory == null) return;

        RefreshTabs();
        RefreshRules();
        RefreshSharedStorage();
        RefreshSelectionInfo();
    }

    void RefreshTabs()
    {
        SetButtonTone(enhanceTab, mode == ForgeMode.Enhance);
        SetButtonTone(synthesisTab, mode == ForgeMode.Synthesis);
        SetButtonTone(dismantleTab, mode == ForgeMode.Dismantle);
    }

    void SetButtonTone(Button button, bool selected)
    {
        if (button == null) return;
        Image image = button.targetGraphic as Image;
        if (image == null) return;

        Sprite skin = selected ? tabSelectedSkin : tabNormalSkin;
        if (skin != null)
        {
            image.sprite = skin;
            image.type = Image.Type.Sliced;
            image.color = Color.white;
        }
        else
        {
            image.color = selected
                ? new Color(.34f, .16f, .055f, 1f)
                : new Color(.105f, .072f, .060f, 1f);
        }

        Text label = button.GetComponentInChildren<Text>();
        if (label != null)
            label.color = selected
                ? new Color(1f, .90f, .60f, 1f)
                : new Color(.91f, .84f, .73f, 1f);
    }

    void RefreshRules()
    {
        if (ruleText == null) return;

        if (mode == ForgeMode.Enhance)
            ruleText.text = "제물가치  -% 1 · +% 2 · 전용 3 · 수식어 5  |  안전 강화: 가치×10%";
        else if (mode == ForgeMode.Synthesis)
            ruleText.text = "대상 블록 + 제물 블록 1개  ·  기본 성공률 1%  ·  제물의 강화 포함 현재 공/방/체 전부 합산";
        else
            ruleText.text = "제물가치  -% 1 · +% 2 · 전용 3 · 수식어 5  |  분해: 가치×10%";
    }

    void RefreshSharedStorage()
    {
        int heldCount = inventory.GetStorageCount(XTapGearBlockData.LocationHeld, IsForgeListVisible);
        int groundCount = inventory.GetStorageCount(XTapGearBlockData.LocationGround, IsForgeListVisible);
        int heldPages = inventory.GetStoragePageCount(XTapGearBlockData.LocationHeld, IsForgeListVisible);
        int groundPages = inventory.GetStoragePageCount(XTapGearBlockData.LocationGround, IsForgeListVisible);

        heldPage = Mathf.Clamp(heldPage, 0, heldPages - 1);
        groundPage = Mathf.Clamp(groundPage, 0, groundPages - 1);

        if (heldCountText != null) heldCountText.text = "소지품    " + heldCount + "개";
        if (groundCountText != null) groundCountText.text = "바닥    " + groundCount + "개";
        if (heldPageText != null) heldPageText.text = heldCount == 0 ? "0 / 0" : (heldPage + 1) + " / " + heldPages;
        if (groundPageText != null) groundPageText.text = groundCount == 0 ? "0 / 0" : (groundPage + 1) + " / " + groundPages;

        if (heldPrevButton != null) heldPrevButton.interactable = heldPage > 0;
        if (heldNextButton != null) heldNextButton.interactable = heldCount > 0 && heldPage < heldPages - 1;
        if (groundPrevButton != null) groundPrevButton.interactable = groundPage > 0;
        if (groundNextButton != null) groundNextButton.interactable = groundCount > 0 && groundPage < groundPages - 1;

        inventory.RenderSharedStoragePage(
            XTapGearBlockData.LocationHeld,
            heldContent,
            heldPage,
            OnItemPressed,
            IsForgeSelected,
            IsForgeListVisible
        );
        inventory.RenderSharedStoragePage(
            XTapGearBlockData.LocationGround,
            groundContent,
            groundPage,
            OnItemPressed,
            IsForgeSelected,
            IsForgeListVisible
        );
    }

    bool IsForgeSelected(string id)
    {
        return targetId == id || materialIds.Contains(id);
    }

    bool IsForgeListVisible(string id)
    {
        return !IsForgeSelected(id);
    }

    void ClearTargetSelection()
    {
        if (probabilityBusy || string.IsNullOrEmpty(targetId)) return;
        targetId = null;
        Refresh();
    }

    void ClearMaterialSelection()
    {
        if (probabilityBusy || materialIds.Count == 0) return;
        materialIds.Clear();
        Refresh();
    }

    void ChangeStoragePage(int location, int delta)
    {
        if (location == XTapGearBlockData.LocationHeld)
            heldPage = Mathf.Max(0, heldPage + delta);
        else if (location == XTapGearBlockData.LocationGround)
            groundPage = Mathf.Max(0, groundPage + delta);

        RefreshSharedStorage();
    }

    string LocationName(XTapGearBlockData item)
    {
        if (item == null) return "없음";
        if (item.location == XTapGearBlockData.LocationHeld) return "소지품";
        if (item.location == XTapGearBlockData.LocationGround) return "바닥";
        return "장착";
    }

    void OnItemPressed(string id)
    {
        if (string.IsNullOrEmpty(id)) return;
        XTapGearBlockData pressed = inventory.FindForgeItem(id);
        if (pressed == null) return;
        if (pressed.location != XTapGearBlockData.LocationHeld &&
            pressed.location != XTapGearBlockData.LocationGround)
            return;

        if (mode == ForgeMode.Dismantle)
        {
            ToggleMaterial(id, 10);
            Refresh();
            return;
        }

        if (string.IsNullOrEmpty(targetId))
        {
            targetId = id;
            materialIds.Remove(id);
            Refresh();
            return;
        }

        if (targetId == id)
        {
            targetId = null;
            materialIds.Remove(id);
            Refresh();
            return;
        }

        ToggleMaterial(id, mode == ForgeMode.Synthesis ? 1 : 10);
        Refresh();
    }

    void ToggleMaterial(string id, int maxValue)
    {
        if (materialIds.Contains(id))
        {
            materialIds.Remove(id);
            return;
        }

        if (mode == ForgeMode.Synthesis)
        {
            if (materialIds.Count >= maxValue)
            {
                if (resultText != null)
                    resultText.text = "합성 제물은 1개만 선택할 수 있습니다.";
                return;
            }

            materialIds.Add(id);
            return;
        }

        XTapGearBlockData material = inventory.FindForgeItem(id);
        if (material == null) return;

        int currentValue = GetSelectedSacrificeValue();
        int addedValue = GetSacrificeValue(material);
        if (currentValue + addedValue > maxValue)
        {
            if (resultText != null)
                resultText.text =
                    "제물 가치는 최대 " + maxValue + "입니다.  현재 " +
                    currentValue + " + 선택 블럭 " + addedValue;
            return;
        }

        materialIds.Add(id);
    }

    int GetSacrificeValue(XTapGearBlockData item)
    {
        if (item == null) return 0;

        // Highest applicable tier wins so a rare block is never valued below
        // one of its lower traits.
        if (item.descriptorCount > 0) return 5;
        if (item.exclusive) return 3;
        if (item.correction > 0) return 2;

        // Negative-correction blocks are worth 1. A plain 0% non-exclusive
        // ticket block also falls back to 1 so every valid block can be used.
        return 1;
    }

    int GetSelectedSacrificeValue()
    {
        int total = 0;
        for (int i = 0; i < materialIds.Count; i++)
        {
            XTapGearBlockData item = inventory.FindForgeItem(materialIds[i]);
            total += GetSacrificeValue(item);
        }
        return total;
    }

    void RefreshSelectionInfo()
    {
        XTapGearBlockData target = inventory.FindForgeItem(targetId);

        if (targetSlot != null)
            targetSlot.gameObject.SetActive(mode != ForgeMode.Dismantle);

        if (materialSlot != null)
        {
            if (mode == ForgeMode.Dismantle)
                Anchor(materialSlot, .18f, .595f, .82f, .805f);
            else
                Anchor(materialSlot, .535f, .595f, .945f, .805f);
        }

        if (targetSlotText != null)
        {
            targetSlotText.text = target == null
                ? "+"
                : XTapGearNameColor.Rich(target) +
                  (target.enhanceLevel > 0 ? "  +" + target.enhanceLevel : "") +
                  "\n" + XTapStatFormat.BlockTriplet(target.attack, target.defense, target.hp, "   ");
        }

        double sacrificeAttack = 0d;
        double sacrificeDefense = 0d;
        double sacrificeHp = 0d;
        int sacrificeCount = 0;
        int sacrificeValue = 0;
        XTapGearBlockData singleSacrifice = null;

        for (int i = 0; i < materialIds.Count; i++)
        {
            XTapGearBlockData material = inventory.FindForgeItem(materialIds[i]);
            if (material == null) continue;

            sacrificeCount++;
            sacrificeValue += GetSacrificeValue(material);
            sacrificeAttack = XTapStatFormat.SafeAdd(sacrificeAttack, Math.Max(0d, material.attack));
            sacrificeDefense = XTapStatFormat.SafeAdd(sacrificeDefense, Math.Max(0d, material.defense));
            sacrificeHp = XTapStatFormat.SafeAdd(sacrificeHp, Math.Max(0d, material.hp));
            if (sacrificeCount == 1)
                singleSacrifice = material;
        }

        if (materialSlotText != null)
        {
            if (sacrificeCount == 0)
            {
                materialSlotText.text = "+";
            }
            else if (sacrificeCount == 1 && singleSacrifice != null)
            {
                materialSlotText.text =
                    XTapGearNameColor.Rich(singleSacrifice) +
                    (singleSacrifice.enhanceLevel > 0 ? "  +" + singleSacrifice.enhanceLevel : "") +
                    "\n제물 " + GetSacrificeValue(singleSacrifice) + "개분  ·  " +
                    XTapStatFormat.BlockTriplet(singleSacrifice.attack, singleSacrifice.defense, singleSacrifice.hp, "   ");
            }
            else
            {
                materialSlotText.text =
                    "블럭 " + sacrificeCount + "개  ·  제물가치 " + sacrificeValue + "/10" +
                    "\n합계  " + XTapStatFormat.BlockTriplet(sacrificeAttack, sacrificeDefense, sacrificeHp, "   ");
            }
        }

        if (mode == ForgeMode.Enhance)
        {
            bool destructive = target != null && target.enhanceLevel >= 10;
            int chance = destructive ? 10 : Mathf.Clamp(sacrificeValue * 10, 0, 100);
            selectionText.text = destructive
                ? "파괴 강화  +" + (target.enhanceLevel + 1) + "  ·  제물가치 " + sacrificeValue
                : "안전 강화  +" + (target != null ? target.enhanceLevel + 1 : 1) + "  ·  제물가치 " + sacrificeValue + "/10";
            chanceText.text = destructive
                ? "기본 10% · 실패 시 파괴"
                : "기본 " + chance + "% · 실패 시 유지";
            executeButton.interactable = target != null && sacrificeValue > 0 && target.enhanceLevel < 20;
        }
        else if (mode == ForgeMode.Synthesis)
        {
            selectionText.text = sacrificeCount == 1 ? "대상 + 제물 준비" : "대상 + 제물 1개";
            chanceText.text = "기본 성공률 1%";
            executeButton.interactable = target != null && sacrificeCount == 1;
        }
        else
        {
            int chance = Mathf.Clamp(sacrificeValue * 10, 0, 100);
            selectionText.text = "제물가치 " + sacrificeValue + "/10  ·  블럭 " + sacrificeCount + "개";
            chanceText.text = "기본 가방 +1칸  " + chance + "%";
            executeButton.interactable = sacrificeValue > 0;
        }
    }

    string ItemName(string id)
    {
        XTapGearBlockData item = inventory.FindForgeItem(id);
        return item == null ? "없음" : item.displayName;
    }

    void Execute()
    {
        if (inventory == null || probabilityBusy) return;

        int chance;
        if (!TryGetCurrentOperationChance(out chance))
            return;

        if (probabilityRoutine != null)
            StopCoroutine(probabilityRoutine);

        probabilityRoutine = StartCoroutine(RunProbabilityMachine(mode, chance));
    }

    bool TryGetCurrentOperationChance(out int chance)
    {
        chance = 0;
        int sacrificeValue = GetSelectedSacrificeValue();

        if (mode == ForgeMode.Enhance)
        {
            XTapGearBlockData target = inventory.FindForgeItem(targetId);
            if (target == null || materialIds.Count == 0)
            {
                resultText.text = "대상과 재료를 선택하세요.";
                return false;
            }

            if (target.enhanceLevel >= 20)
            {
                resultText.text = "이미 +20 최대 강화입니다.";
                return false;
            }

            chance = target.enhanceLevel >= 10
                ? 10
                : Mathf.Clamp(sacrificeValue * 10, 0, 100);
            return true;
        }

        if (mode == ForgeMode.Synthesis)
        {
            XTapGearBlockData target = inventory.FindForgeItem(targetId);
            if (target == null || materialIds.Count != 1)
            {
                resultText.text = "대상 1개와 재료 1개를 선택하세요.";
                return false;
            }

            XTapGearBlockData material = inventory.FindForgeItem(materialIds[0]);
            if (material == null)
            {
                resultText.text = "재료 블록을 찾을 수 없습니다.";
                ClearSelection();
                Refresh();
                return false;
            }

            chance = 1;
            return true;
        }

        if (materialIds.Count <= 0)
        {
            resultText.text = "분해할 재료를 선택하세요.";
            return false;
        }

        chance = Mathf.Clamp(sacrificeValue * 10, 0, 100);
        return true;
    }

    IEnumerator RunProbabilityMachine(ForgeMode operationMode, int chance)
    {
        probabilityBusy = true;
        if (executeButton != null) executeButton.interactable = false;
        probabilityOverlay.SetActive(true);
        probabilityOverlay.transform.SetAsLastSibling();

        string operationName = operationMode == ForgeMode.Enhance ? "강화" :
            (operationMode == ForgeMode.Synthesis ? "합성" : "분해");
        XTapGearBlockData target = operationMode == ForgeMode.Enhance
            ? inventory.FindForgeItem(targetId) : null;
        bool destructive = target != null && target.enhanceLevel >= 10;
        XTapGearBlockData displayedBlock = target;
        if (displayedBlock == null && materialIds.Count > 0)
            displayedBlock = inventory.FindForgeItem(materialIds[0]);

        // Independent 1% reversal, conditional on whichever finisher was selected.
        // Both rolls and the cosmetic line are fixed before any animation starts.
        XTapForgeOutcome outcome = XTapForgeOutcome.FromRolls(chance,
            UnityEngine.Random.Range(0, 100), UnityEngine.Random.Range(0, 100));
        string[] lines = outcome.AngelFinisher ? XTapForgeDialogue.AngelFinishers : XTapForgeDialogue.DemonFinishers;
        string finisherLine = lines[UnityEngine.Random.Range(0, lines.Length)];
        string reward = operationMode == ForgeMode.Dismantle ? "가방 배치칸 +" + outcome.RewardMultiplier :
            (operationMode == ForgeMode.Synthesis ? "제물 능력 흡수 ×" + outcome.RewardMultiplier :
            "강화 +" + (target == null ? 0 : outcome.EnhancedLevel(target.enhanceLevel)) + "  ·  최대 +20");
        bool applied = false;
        Action applyOutcome = delegate
        {
            if (applied) return;
            applied = true;
            if (operationMode == ForgeMode.Enhance) ResolveEnhance(outcome);
            else if (operationMode == ForgeMode.Synthesis) ResolveSynthesis(outcome);
            else ResolveDismantle(outcome);
            if (outcome.Reversal)
                resultText.text = (outcome.AngelFinisher ? "천사 실수! " : "악마 반전 · 보상 2배! ") + resultText.text;
            Debug.Log("X탑 FORGE_RESULT / mode=" + operationMode + " / baseChance=" + chance +
                " / angelFinisher=" + outcome.AngelFinisher + " / reversal=" + outcome.Reversal +
                " / success=" + outcome.Success + " / rewardMultiplier=" + outcome.RewardMultiplier);
        };
        try
        {
            yield return duelView.Play(operationName, chance, outcome, destructive,
                displayedBlock, GetSelectedSacrificeValue(), finisherLine, reward, applyOutcome);
        }
        finally
        {
            probabilityOverlay.SetActive(false);
            probabilityBusy = false;
            probabilityRoutine = null;
            Refresh();
        }
    }

    void ResolveEnhance(XTapForgeOutcome outcome)
    {
        XTapGearBlockData target = inventory.FindForgeItem(targetId);
        if (target == null)
        {
            resultText.text = "강화 대상을 찾을 수 없습니다.";
            ClearSelection();
            return;
        }

        int materialCount = materialIds.Count;
        bool destructive = target.enhanceLevel >= 10;
        inventory.EnsureEnhancementBaseStats(target);
        ConsumeMaterials();

        if (outcome.Success)
        {
            target.enhanceLevel = outcome.EnhancedLevel(target.enhanceLevel);
            inventory.RecalculateEnhancedStats(target);
            inventory.CommitForgeChanges();
            resultText.text =
                "강화 성공!  +" + target.enhanceLevel + "   " +
                XTapStatFormat.BlockTriplet(target.attack, target.defense, target.hp, " / ");
        }
        else if (destructive)
        {
            string destroyedName = XTapGearNameColor.Rich(target);
            inventory.RemoveForgeItem(target.id);
            inventory.CommitForgeChanges();
            resultText.text =
                "강화 실패. " + destroyedName + "이(가) 파괴되었습니다.  제물 " +
                materialCount + "개 소모";
        }
        else
        {
            inventory.CommitForgeChanges();
            resultText.text =
                "강화 실패. 대상은 유지됩니다.  제물 " + materialCount + "개 소모";
        }

        ClearSelection();
    }

    void ResolveSynthesis(XTapForgeOutcome outcome)
    {
        XTapGearBlockData target = inventory.FindForgeItem(targetId);
        XTapGearBlockData material = materialIds.Count == 1
            ? inventory.FindForgeItem(materialIds[0])
            : null;

        if (target == null || material == null)
        {
            resultText.text = "합성 대상 또는 재료를 찾을 수 없습니다.";
            ClearSelection();
            return;
        }

        double addAttack;
        double addDefense;
        double addHp;
        inventory.GetPreDescriptorStats(material, out addAttack, out addDefense, out addHp);
        string consumedName = XTapGearNameColor.Rich(material);

        inventory.RemoveForgeItem(material.id);

        if (outcome.Success)
        {
            if (outcome.RewardMultiplier == 2)
            {
                addAttack = XTapStatFormat.SafeAdd(addAttack, addAttack);
                addDefense = XTapStatFormat.SafeAdd(addDefense, addDefense);
                addHp = XTapStatFormat.SafeAdd(addHp, addHp);
            }
            inventory.MergeSynthesisStats(target, addAttack, addDefense, addHp);
            inventory.CommitForgeChanges();

            resultText.text =
                "합성 성공!  " + consumedName + " 능력을 흡수했습니다.  합성 후 " +
                XTapStatFormat.BlockTriplet(target.attack, target.defense, target.hp, " / ");
        }
        else
        {
            inventory.CommitForgeChanges();
            resultText.text = "합성 실패. " + consumedName + "이(가) 소모됐습니다.";
        }

        ClearSelection();
    }

    void ResolveDismantle(XTapForgeOutcome outcome)
    {
        int count = materialIds.Count;
        ConsumeMaterials();

        if (outcome.Success)
        {
            inventory.AddGridCellExpansion(outcome.RewardMultiplier);
            resultText.text =
                "분해 성공! 가방 배치칸 +" + outcome.RewardMultiplier + ".  현재 " + inventory.GridCapacity + "칸";
        }
        else
        {
            inventory.CommitForgeChanges();
            resultText.text =
                "분해 실패. 재료 " + count + "개가 모두 소모됐습니다.";
        }

        ClearSelection();
    }

    void ConsumeMaterials()
    {
        string[] ids = materialIds.ToArray();

        for (int i = 0; i < ids.Length; i++)
        {
            if (ids[i] == targetId) continue;
            inventory.RemoveForgeItem(ids[i]);
        }
    }

    void LoadVisualAssets()
    {
        try
        {
            byte[] bytes = null;
            TextAsset encoded = Resources.Load<TextAsset>("XTapBlacksmithUI/atlas");

            if (encoded != null && !string.IsNullOrWhiteSpace(encoded.text))
            {
                try
                {
                    bytes = Convert.FromBase64String(encoded.text.Trim());
                }
                catch
                {
                    bytes = null;
                }
            }

            // 11.40: guaranteed runtime fallback for the existing blacksmith atlas.
            if (bytes == null || bytes.Length < 1024)
            {
                bytes = Convert.FromBase64String(EmbeddedBlacksmithAtlasPngBase64);
                Debug.LogWarning("X탑 대장간 atlas Resources 로드 실패. 내장 이미지 fallback 사용.");
            }

            skinAtlas = new Texture2D(2, 2, TextureFormat.RGB24, false);
            if (!skinAtlas.LoadImage(bytes, false))
            {
                Destroy(skinAtlas);
                skinAtlas = null;
                Debug.LogError("X탑 대장간 atlas 이미지 최종 로드 실패.");
                return;
            }

            skinAtlas.wrapMode = TextureWrapMode.Clamp;
            skinAtlas.filterMode = FilterMode.Bilinear;

            // Atlas is 256x512. Coordinates below use top-left design coordinates.
            backgroundSkin = MakeAtlasSprite(0, 0, 144, 256, Vector4.zero);
            panelSkin = MakeAtlasSprite(144, 0, 112, 84, new Vector4(18f, 18f, 18f, 18f));
            slotSkin = MakeAtlasSprite(144, 84, 112, 112, new Vector4(18f, 18f, 18f, 18f));
            tabNormalSkin = MakeAtlasSprite(0, 256, 128, 43, new Vector4(22f, 10f, 22f, 10f));
            tabSelectedSkin = MakeAtlasSprite(128, 256, 128, 43, new Vector4(22f, 10f, 22f, 10f));
            buttonNeutralSkin = MakeAtlasSprite(0, 299, 128, 43, new Vector4(22f, 10f, 22f, 10f));
            buttonPrimarySkin = MakeAtlasSprite(128, 299, 128, 43, new Vector4(22f, 10f, 22f, 10f));
            statusBarSkin = MakeAtlasSprite(0, 406, 256, 64, new Vector4(26f, 12f, 26f, 12f));
        }
        catch (Exception e)
        {
            Debug.LogError("X탑 대장간 UI 에셋 로드 실패: " + e.Message);
        }
    }

    Sprite MakeAtlasSprite(int x, int yFromTop, int width, int height, Vector4 border)
    {
        if (skinAtlas == null) return null;

        int y = skinAtlas.height - yFromTop - height;
        Rect rect = new Rect(x, y, width, height);
        return Sprite.Create(
            skinAtlas,
            rect,
            new Vector2(.5f, .5f),
            100f,
            0,
            SpriteMeshType.FullRect,
            border
        );
    }

    void ApplyPanelSkin(RectTransform rect, Sprite skin)
    {
        if (rect == null || skin == null) return;
        Image image = rect.GetComponent<Image>();
        if (image == null) return;

        image.sprite = skin;
        image.type = Image.Type.Sliced;
        image.color = Color.white;
    }

    void ApplyButtonSkin(Button button, Sprite skin)
    {
        if (button == null || skin == null) return;
        Image image = button.targetGraphic as Image;
        if (image == null) return;

        image.sprite = skin;
        image.type = Image.Type.Sliced;
        image.color = Color.white;
    }

    RectTransform MakePanel(Transform parent, string name, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);

        Image image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image.rectTransform;
    }

    Button MakeButton(Transform parent, string label, int fontSize, Color color)
    {
        GameObject go = new GameObject(label + "Button", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);

        Image bg = go.GetComponent<Image>();
        bg.color = color;

        RectTransform accent = MakePanel(go.transform, "TypeAccent", new Color(.72f, .43f, .18f, .95f));
        Anchor(accent, .18f, .90f, .82f, .925f);
        accent.GetComponent<Image>().raycastTarget = false;

        Text t = MakeText(go.transform, label, fontSize + 1, TextAnchor.MiddleCenter, true);
        t.color = new Color(1f, .91f, .78f, 1f);
        Outline outline = t.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, .90f);
        outline.effectDistance = new Vector2(2f, -2f);
        Anchor(t.rectTransform, .04f, .04f, .96f, .90f);

        Button b = go.GetComponent<Button>();
        b.targetGraphic = bg;

        ColorBlock cb = b.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(1f, .92f, .78f, 1f);
        cb.pressedColor = new Color(.62f, .43f, .30f, 1f);
        cb.selectedColor = Color.white;
        cb.fadeDuration = .06f;
        b.colors = cb;

        return b;
    }

    Text MakeText(Transform parent, string value, int size, TextAnchor align, bool bold)
    {
        GameObject go = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(parent, false);

        Text t = go.GetComponent<Text>();
        t.text = value;
        t.font = font;
        t.fontSize = Mathf.RoundToInt(size * UiFontScale);
        t.alignment = align;
        t.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
        t.supportRichText = true;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Truncate;
        t.raycastTarget = false;
        return t;
    }

    void Frame(RectTransform parent, Color color, float thickness)
    {
        RectTransform top = MakePanel(parent, "FrameTop", color);
        Anchor(top, 0f, 1f, 1f, 1f);
        top.sizeDelta = new Vector2(0f, thickness);

        RectTransform bottom = MakePanel(parent, "FrameBottom", color);
        Anchor(bottom, 0f, 0f, 1f, 0f);
        bottom.sizeDelta = new Vector2(0f, thickness);

        RectTransform left = MakePanel(parent, "FrameLeft", color);
        Anchor(left, 0f, 0f, 0f, 1f);
        left.sizeDelta = new Vector2(thickness, 0f);

        RectTransform right = MakePanel(parent, "FrameRight", color);
        Anchor(right, 1f, 0f, 1f, 1f);
        right.sizeDelta = new Vector2(thickness, 0f);
    }

    static void Anchor(RectTransform r, float x1, float y1, float x2, float y2)
    {
        r.anchorMin = new Vector2(x1, y1);
        r.anchorMax = new Vector2(x2, y2);
        r.offsetMin = Vector2.zero;
        r.offsetMax = Vector2.zero;
    }
}
