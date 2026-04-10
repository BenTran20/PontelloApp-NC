function startTour() {
    introJs().setOptions({
        steps: [
            {
                element: '#archivebtn',
                intro: 'You can inactive order by pressing the Archive button.'
            },
            {
                element: '#archivedProducts',
                intro: 'Once the archive button is pressed, that order will be hidden from other dealers will be archived in Archvied Products.'
            },
        ]
    }).start();
}